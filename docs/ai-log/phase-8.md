# AI log: Phase 8 (final review, deliverable documents)

- **Date:** 2026-09-28
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:**
  - project files: `CLAUDE.md`, `docs/REQUIREMENTS-MATRIX.md` (in full), `docs/ARCHITECTURE-PLAN.md`, `docs/REVIEW-PREP.md`, `docs/DEPLOYMENT.md`,
    `docs/ai-log/phase-0.md` … `phase-7.md`, the `docs/DECISIONS.md` index plus D-06, D-40 item 6 and the D-41..D-44 statuses;
  - specs: brief §3–§8 and Appendix A; FRS §3, §4, §5.3–5.4, §6, §7, §8, §12–§14;
  - the domain, application, persistence, API and Angular code named below.

## 1. Prompt given (verbatim: Phase 8 from `docs/PROMPTS.md`)

```
Phase 8: final review. Act as a strict DICEUS reviewer using brief §6 and §6.1 as the rubric.
1. Walk the REQUIREMENTS-MATRIX row by row and verify each requirement against code AND a test. Mark Done,
   Partial, or Missing with evidence (file:line / test name). Do not mark anything Done without evidence.
2. Check the §6.1 list explicitly: MediatR read/write separation; domain events → audit; validation in the
   pipeline; value conversions, shadow properties, global filters (soft delete + tenant); UoW; AutoMapper in
   Application; Hangfire idempotency; storage behind an interface.
3. Report hallucinations or domain errors you find in our own code (wrong thresholds, ≤ vs <, missed roles,
   wrong messages), and fix them only after I approve.
4. Draft README.md (all six sections from brief §4.2), ARCHITECTURE.md (all items from brief §4.4, with a Mermaid
   solution + data-model diagram), and the AI-WORKFLOW.md skeleton (brief §4.5). Use only facts from docs/ai-log/
   and DECISIONS.md for the "what AI got wrong" section, and leave TODO markers where I must write it myself.
5. Finalise REVIEW-PREP.md: 25 likely deep-dive questions with concise, code-referenced answers, plus 5
   rehearsal "live implementation" tasks (e.g. a new validation rule, a new query endpoint, a new filter on the
   claims list, a multi-currency sketch) with the files to touch.
```

## 2. How the matrix was verified (item 1)

- **Test names.** A script extracted all 473 test names cited in the matrix and resolved each one to a real test: an xUnit method, or a
  Vitest `it`/`it.each` title. **All 473 exist.** The five that the first pass reported missing were three `it.each` titles with `%s`
  placeholders and two index names, `UX_ReserveHistory_*`. All five were confirmed by grep.
- **Code locations.** A second script maps each of the 224 rows to the code that enforces it. Each location is a regex that must match in
  the file, and the script fails if it does not, so a line number cannot be invented. On the first run, 21 patterns did not match: guessed
  symbol names, or markup that lives in `.html` rather than `.ts`. All were corrected against the real files before anything was written.
  Seventeen resolved lines were then spot-checked by eye; four pointed at a weaker line (for example a comment) and were moved to the
  deciding line.
- **Test runs.**
  - `dotnet build` passed with 0 errors.
  - `dotnet test` passed **692/692** (324 domain, 89 application, 279 integration).
  - `ng lint` was clean, `ng test --watch=false` passed **95/95**, and `ng build` succeeded, emitting the three lazy route chunks.
- **Other checks.**
  - `dotnet ef database update` built a fresh scratch database (`ClaimsModule_P8Readme`) with 8 policies, 10 codes, 12 transitions, 6 users
    and 3 migrations. The database was dropped afterwards.
  - The deployed API (`/health/live`, `/swagger/v1/swagger.json`) and the SPA answered 200.
  - A grep found no GUID literal outside `Seed/` and `Migrations/` (CONV-12).
- **Result:** **203 Done · 20 Partial · 1 Missing.**
  - Partial means the code is present but the only evidence is manual. That covers 15 UI rows checked in the browser in Phase 6, API-LOG,
    JOB-13 (the delete itself is untested), CONV-12 (grep, no test), CONV-13 (table and route naming by review) and DEL-03 (a skeleton with
    TODOs).
  - Missing is DEL-04: no exported AI session in the repo.

## 3. Brief §6.1 checklist (item 2)

| §6.1 item | Verdict | Evidence |
|---|---|---|
| CQRS separates reads from writes via MediatR | ✅ | `ICommand<T>` / `IQuery<T>` (`Application/Common/Messaging`). `UnitOfWorkBehavior.cs:20` runs for commands only. Queries use SQL projections behind `IClaimQueries`. Tests: `CONV_13_*`, `CONV_15_Queries_bypass_the_unit_of_work_and_commands_use_it` |
| Domain events raised and handled (ClaimCreated → audit) | ✅ | 21 events. `UnitOfWork.cs:81-103` handles before-commit dispatch. `ClaimAuditTrail` implements `IBeforeCommitHandler<ClaimCreated>` and the other 20. Tests: `AUD_I1_Every_domain_event_has_a_before_commit_audit_handler`, `AUD_01_Creation_writes_the_audit_trail_with_the_request_correlation_id` |
| FluentValidation at the MediatR pipeline level | ✅ | `ValidationBehavior.cs:26-41`, registered at `Application/DependencyInjection.cs:22`. Controllers contain no validation. Tests: `CONV_15_Validation_behavior_short_circuits_handler`, `API_ERR_422_body_matches_FRS_10_4_exactly` |
| EF value conversions, shadow properties, global filters (soft delete + tenant) | ✅ | `ClaimsDbContext.cs:60-71` (decimal 19,4; precision 7; enums → NVARCHAR(50)). `ModelBuilderConventions.cs:43-65` (shadow columns). `ClaimsDbContext.cs:78-96` (combined filter). Tests: `CONV_02..05`, `SEC_04_*` |
| Unit of Work coordinates transactions across repositories | ✅ | `UnitOfWork.cs:35-62`: execution strategy, BEGIN, handler, before-commit events, SaveChanges, COMMIT, then after-commit events. Test: `AUD_I4_A_failing_command_leaves_no_claim_no_audit_row_and_no_used_claim_number` |
| AutoMapper profiles in Application, not API | ✅ | `Application/Claims/ClaimMappingProfile.cs:11`, `Application/Users/UserMappingProfile.cs:6`. No `Profile` exists in the API project. Test: `CONV_15_AutoMapper_profiles_live_only_in_Application` |
| Hangfire idempotency key strategy | ✅ | `GlIdempotencyKey.cs:13` gives `Reserve:{componentId}:Change:{seq}`. `GlPostingStore.cs:27-38, 71-77` is a single-statement compare-and-set on `Pending`. Unique indexes back it up. Tests: `BR_R_06_*` (3), `JOB_02_*` |
| Blob storage behind an interface | ✅ | `IStorageService` (`Application/Abstractions/IStorageService.cs:9`), with Azure and local implementations chosen by `Storage:Provider` (`StorageRegistration.cs:33-36`). Tests: `BR_D_03_Provider_is_selected_from_configuration`, and Azurite/local tests of both implementations |

## 4. Findings in our own code and docs (item 3); F1–F3 applied after Vlad's approval (see §7)

The rules checked and found **correct**:
- **Authority tiers:** `≤ 10,000` / `≤ 100,000` on `|amount|` (`ReserveAuthorityPolicy.cs:18,23`).
- **$10M limit:** `>` the limit, so exactly $10M is allowed; cost components only; increases only (`Claim.Reserves.cs:313-317`).
- **SLA:** stale when strictly older than 48h (`<`), repeated after 24h or more (`SlaMonitoringQueries.cs:30,34`); Draft/Open only.
- **Loss date and description:** `lossDate <= now`; description of at least 20 trimmed characters.
- **Messages:** all 14 FRS §8 messages verbatim (`DomainMessages.cs:13-30`).
- **Roles:**
  - approve/reject need Supervisor+ at the endpoint, then the tier in the domain;
  - reopen needs Supervisor+ (hierarchical);
  - the override is Manager-only in both the endpoint and the domain;
  - the Hangfire dashboard is Manager-only.
- **Audit:** RESERVE_REJECTED puts the reason in OldValue, as FRS §14.1 says literally.
- **Closure:** CC-04 is `CurrentAmount > 0`.
- **GL job:** the compare-and-set is on `= 'Pending'`; retries are 3, delays 10/30/60 s.
- **Jobs and UI:** the SLA cron is `*/15 * * * *`; badge colours match FRS §11.1; the indicator labels match FRS §11.2.

| # | Kind | Finding | Where | Proposal |
|---|---|---|---|---|
| F1 | Spec reconciliation | FRS §5.4 gives "Policy not found" as a **Warning** example. A POST with a `policyId` that does not exist gets **422** "Policy was not found.". D-40 item 6 decides "an unknown id in the body → 422" in general, but never cites §5.4 | `CreateClaimCommandValidator.cs:37-40`, `CreateClaimCommandHandler.cs:38` | **Docs only (recommended):** amend D-40 item 6 to say that the §5.4 warning is BR-C-06 "No policy linked" (the Unknown Policy toggle, `policyId = null`), while an id that matches no policy is a malformed request. Alternative: create the claim with `PolicyId = null` and a warning (a behaviour change; it hides client bugs) |
| F2 | Spec gap | A **submitter with supervisor+ role can reject their own pending transaction**. BR-R-03 forbids only self-*approval*. FRS §6.4 gives the submitter *retract* (→ Cancelled). The effect is the same as a retract, but the audit says RESERVE_REJECTED, which muddles four-eyes semantics | `Claim.Reserves.cs:147-170`; UI `reserve-actions.ts:49-51` | Record as D-45. Option (b), recommended: refuse self-rejection with 422 "Use Retract to withdraw your own pending reserve." (domain + UI blocked reason + `Dom`/`Int`/`Web` tests). Option (a): keep it as FRS-literal and document it |
| F3 | UI parity | The Add Reserve **authority preview ignores the BR-R-05 escalation**. On a claim with $9,998,000 approved, a $5,000 increase previews "✓ Auto-approved (≤ $10,000)", but the API makes it PendingApproval / Manager and returns the warning. The API is right; the preview misleads. FNOL is not affected: an initial reserve can only cross $10M above $100k, which already previews Manager | `web/claims-ui/src/app/shared/domain/authority.ts:11-21`, `add-reserve-panel.html:69` | Pass `approvedAggregate`, `aggregateLimit`, the override flag and the component into the preview (the DTO already carries them); add `Web: RSV_09_*` cases. This is REVIEW-PREP task 4 |
| F4 | Stale doc (fixed, docs only) | REVIEW-PREP's Phase 1 answer said the correlation id accepts "1–64 characters of `[A-Za-z0-9-_.]`". Since D-39 Q1 the middleware accepts GUIDs only (`CorrelationIdMiddleware.cs:34`), and REVIEW-PREP's own Phase 2 answer said so, so the file contradicted itself | `docs/REVIEW-PREP.md` appendix | Fixed while finalising REVIEW-PREP (item 5). The header "Code references are added once the code exists" was replaced too |
| F5 | Process | The numbered items of **D-38 and D-40..D-44 are still PROPOSED**. Only their Q-questions were accepted. A reviewer reading DECISIONS.md sees open decisions in shipped code | `docs/DECISIONS.md` index | Vlad to accept or amend them before submission |
| F6 | Delivery | **DEL-04 is Missing**: no exported AI conversation in the repo (brief §4.6). AI-WORKFLOW.md has TODO sections only Vlad can write | `docs/ai-log/` | Export the sessions to `docs/ai-log/exports/`; fill in the AI-WORKFLOW TODOs |
| F7 | Test gaps | The 20 Partial rows. The cheapest to close: a CONV-12 test (scan `src/` outside `Seed/` and `Migrations/` for GUID literals), a JOB-13 test (FakeTimeProvider + one old and one new record), and component tests for the manual UI rows | matrix | Optional; each is small |

## 5. What was generated

| File | Content |
|---|---|
| `docs/REQUIREMENTS-MATRIX.md` | Every row from §2 on now has a Done/Partial/Missing status and its code location (`file:line`). The test column is renamed "Tests (all exist and pass, P8)". The header records how this was verified. §1 (the brief→FRS mapping) is untouched: earlier phases had rewritten it by accident (phase-2 §6 item 10, phase-3 §6 item 11), so the script starts at §2 |
| `README.md` | Brief §4.2 sections 1–6. Deployed URLs and test credentials first. Every command was taken from files in the repo; the migration command was run against a scratch database |
| `ARCHITECTURE.md` | Brief §4.4, all 7 items, with 4 Mermaid diagrams: solution/dependency flowchart, data-model ER diagram, CQRS sequence (approve reserve), Azure topology. Built from ARCHITECTURE-PLAN.md, updated to the delivered state (21 events, final Hangfire design, D-44 Azure) |
| `AI-WORKFLOW.md` | Skeleton for brief §4.5: tools, workflow, 4 verbatim prompts, the AI-generated vs designed split (facts only), 12 corrections, each cited to `docs/ai-log/phase-N.md`, and TODO blocks for Vlad |
| `docs/REVIEW-PREP.md` | Part 1: 25 questions with code references. Part 2: 5 rehearsal tasks with the files to touch. Appendix: the Phase 0–7 bank, headings demoted, F4 fixed |
| `docs/ai-log/phase-8.md` | this file |

## 6. What Claude got wrong in this phase (honest record)

1. **Ran the test suite on a runtime that is not installed.** The first `dotnet test` aborted every test host: the host has only the .NET 10
   runtime. The phase logs already said `DOTNET_ROLL_FORWARD=Major`; Claude re-ran with it.
2. **`npm test -- --run`**: that is a Vitest flag, but the script is `ng test`, which rejected it ("Unknown argument: run"). Re-run as
   `ng test --watch=false`, the form CI uses.
3. **Guessed symbol names in the first evidence map.** 21 of 224 patterns did not match, e.g. `ListClaimsAsync` (the method is `ListAsync`),
   `EvaluatePolicyPeriodIssue` looked for in `Claim.cs` (it is in `Claim.Parties.cs`), and template markup looked for in `.ts` files. The
   resolver refused them, so no invented line number reached the matrix.
4. **An overstated fact in the AI-WORKFLOW draft.** It said Vlad accepted "D-41..D-44 Q1–Q3", but D-41 has only Q1 and Q2. It was checked
   against the DECISIONS index and corrected before the summary.
5. A helper shell function named `g` collided with a zsh alias. The shell failed; renamed. No effect on output.

## 7. Vlad's decisions after the summary
Vlad's reply (verbatim):
```
Apply F1, F2 and F3, then merge to main
```
F5–F7 were not chosen, so they stay open.

What was applied:
- **F1 (docs only).** D-40 item 6 amended. The FRS §5.4 "Policy not found" warning is BR-C-06 (a null `policyId`, the Unknown Policy toggle).
  A `policyId` that matches no policy stays 422. The matrix row API-01 now cites it.
- **F2 → D-45 (ACCEPTED).**
  - Domain: `Claim.RejectReserveTransaction` refuses the submitter with 422 "Self-rejection is not permitted. Use Retract to withdraw your own
    pending reserve." under `ReserveApproval`. A supervisor self-rejecting above $100k gets both reasons, as for approval. The new message is
    in `DomainMessages`.
  - UI: `reserveRowActions` disables Reject on the user's own row with the same message.
  - Tests: `Dom: D_45_Self_rejection_is_refused_and_points_to_retract`, `Dom: D_45_Supervisor_self_rejecting_above_100000_gets_both_reasons`,
    `Int: D_45_Self_rejection_returns_422_and_the_submitter_retracts_instead`, `Web: D_45_Self_rejection_is_disabled_and_Retract_is_offered`.
  - Mutation check: with the guard removed, both domain tests fail; with it restored, both pass.
  - D-25's list of 422 cases was cross-referenced. The matrix gains row RSV-10.
- **F3.** `requiredAuthority(amount, context?)` and `exceedsAggregateLimit` in `shared/domain/authority.ts` mirror
  `Claim.WouldExceedAggregateLimit`: cost components only, increases only, `>`, and the override lifts it. `AuthorityIndicator` takes an
  optional `aggregate` input and shows the FRS §8 warning when the transaction escalates. The Add Reserve panel passes the claim's approved
  aggregate, the limit and the override flag from GET /reserves. FNOL is unchanged, because nothing is approved at intake. Four
  `Web: BR_R_05_*` tests were added, and the matrix RSV-09 row updated.
- **Housekeeping.** The new guard shifted line numbers in `Claim.Reserves.cs` and `reserve-actions.ts`. The evidence script was re-run and
  the nine affected matrix cells and the REVIEW-PREP references were updated. The script started at matrix §2, so §1 stayed untouched.
  REVIEW-PREP Q12/Q13 and rehearsal task 4 were updated; task 4's escalation half is now built, so the task became a history-table change.
- **Verification.**
  - `dotnet build`: 0 warnings.
  - `dotnet test`: **695/695** (326 domain, 89 application, 280 integration).
  - `ng lint` was clean, `ng test`: **100/100**, and `ng build` succeeded.
  - All test names in the matrix resolve (480).
- `phase-8-review` merged into `main`.

One slip during the fixes: the first patch of the shifted matrix references matched the same ID in the §1 mapping table and stopped on
an assertion before writing anything. It was re-run starting at §2. This is the third time that table has caught a script
(phase-2 §6 item 10, phase-3 §6 item 11), which is why every matrix script now starts at §2.
