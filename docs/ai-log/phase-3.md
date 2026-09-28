# AI log: Phase 3 (commands, queries, validators, controllers)

- **Dates:** 2026-09-27 → 2026-09-28 (two sessions: the first stopped at a usage limit, the second finished the phase).
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:** `CLAUDE.md`, the full FRS, `docs/DECISIONS.md` (in full), `docs/PROMPTS.md` Phase 3, `docs/ARCHITECTURE-PLAN.md`,
  `docs/REQUIREMENTS-MATRIX.md`, `docs/ai-log/phase-2.md`, brief §3.3–3.5, and all Phase 1–2 source and tests.

## 1. Prompts given (verbatim)

The Phase 3 prompt from `docs/PROMPTS.md`:
```
Phase 3: MediatR commands/queries + FluentValidation + controllers for FNOL, claims, parties, status,
validation issues, policies, and reference data (FRS §10.1, §10.3 and the endpoints added in D-08).
- CreateClaimCommand: single transaction; atomic claim number; LossEvent, parties, risk objects, and optional
  initial reserve (reusing the reserve logic from Phase 4 — stub it behind the same domain method now);
  validation issues persisted per D-06/D-07; ClaimCreated → audit. 201 + Location header.
- ListClaimsQuery with all the FRS filters, pagination metadata, and totalReserves. Project in SQL
  (ProjectTo / Select), with no N+1 queries. GetClaimDetailQuery. GetClaimAuditQuery (reverse chronological, paged).
- TransitionClaimStatusCommand: 422 with valid next statuses or a blocking-conditions list; role checks from
  the transitions table; CC-04 justification; Reopened→Open.
- AddParty / RemoveParty (422 when removing the last Claimant).
- The validators' messages must match FRS §8 word for word.
- Integration tests through HTTP (WebApplicationFactory + Testcontainers) for the happy path and each 422 rule.
Challenge the spec where it is weak, but implement what DECISIONS.md says. Stop and summarise.
```

After the usage-limit stop, Claude listed what was done and left and asked two questions. Vlad's answer (verbatim):
```
1. Yes, add POST /claims/{id}/risk-objects.
2. Keep it here.

Finish what's left for this phase and notify me once we can move to the next one.
```

## 2. Requirement IDs touched
API-01..07, API-16..23, API-26, API-29, new API-30, API-IDEMP, API-ERR, API-AUTH, API-CORR; BR-C-01..07, B-C-06, BR-P-02, PTY-01;
BR-ST-01..04, CC-01, CC-04, TR-01/03/04/05/11/12/13; VAL-01, 02, 04, 05, 08, 09, 11, 12; BR-R-01/02/05 (FNOL initial reserve only);
AUD-01..06, AUD-13, 14, 16, 17 (partial), AUD-I1, AUD-I3, AUD-I4; BR-A-02 (partial); SEC-04; CONV-15.
Decisions applied: D-06, D-07, D-08, D-09, D-10, D-18, D-19, D-24, D-25, D-26, D-27, D-29, D-32, D-33; new D-40 (Q1 and Q2 decided by Vlad, 17 items PROPOSED).

## 3. What was generated
| Area | Content |
|---|---|
| Domain | `FieldLengths` (column sizes shared with the validators); party and risk-object messages moved into `DomainMessages` with unchanged wording; `ClaimParty.IsPlausibleEmail` made public. No rule changed. |
| Application | `UnitOfWorkBehavior` (commands only); `IBeforeCommitHandler`/`IAfterCommitHandler` + `DomainEventDispatcher`; `ClaimAuditTrail` (all 20 events → audit rows); 10 commands (CreateClaim, TransitionClaimStatus, AddParty, RemoveParty, AddRiskObject, ValidateClaim, LinkPolicy, AssignClaimHandler, UpdateClaimDetails, AcknowledgeValidationIssue) and 9 queries with validators; shared party / risk-object / initial-reserve validators; read contracts (`IClaimQueries`, `IReferenceDataQueries`, `IPolicyQueries`, `IUserQueries`); `IIdempotencyStore`; DTOs and AutoMapper profiles |
| Persistence | `UnitOfWork` with the two-phase dispatch; policy and transition repositories, a tenant-scoped user lookup; SQL read models (`ClaimQueries`, reference, policy, user); `IdempotencyRecord` + `IdempotencyStore`; migration `AddIdempotencyRecords` (only the new table: the `FieldLengths` refactor left the model unchanged) |
| API | 7 controllers (claims, parties, risk objects, validation issues, reference data, policies, users); `IdempotencyFilter` (resource filter); lenient enum and date JSON converters; `ConflictException` → 409 |
| Tests | Application 35 (was 12); Integration 177 (was 84): FNOL, list/detail/audit, status machine, parties/risk objects/issues, reference endpoints, idempotency, unit-of-work atomicity, after-commit timing, N+1 counts, audit-write IL scan |
| Docs | D-40, matrix statuses (+ API-30), ARCHITECTURE-PLAN §2.5, §3, §4.1, §5, REVIEW-PREP Phase 3, this log |

**Verification that ran:**
- Host (SDK 10, `DOTNET_ROLL_FORWARD=Major`): `dotnet build` with 0 warnings; `dotnet test` **476/476** (264 domain, 35 application, 177 integration).
- `mcr.microsoft.com/dotnet/sdk:9.0` (SDK 9.0.318), Testcontainers SQL Server 2022 through the host Docker socket: build with 0 warnings, **476/476**.
  This run was before the two test renames in §6 item 10; the final host run after them is the one reported in the phase summary.
- **Mutation checks:**
  - Dispatching after-commit handlers *before* the commit made `CONV_15_After_commit_handlers_run_after_the_commit_and_never_after_a_rollback`
    fail, because the handler saw 0 committed rows.
  - Never storing idempotent responses made `API_IDEMP_Repeated_key_replays_the_response_and_creates_one_claim` and
    `API_IDEMP_Same_key_with_a_different_body_returns_422` fail.
  - Both mutations were reverted.
- Not run this phase: the docker-compose `app` profile smoke test and `ng` (no frontend yet).

## 4. What Vlad decided during the phase
- **Q1:** add `POST /api/claims/{id}/risk-objects`. It was raised because the FRS gives no way to clear the "no risk objects" warning after FNOL.
- **Q2:** keep the Idempotency-Key work in Phase 3. It was raised because D-39 item 18 scheduled it here, but the Phase 3 prompt did not list it.

## 5. Where the spec was challenged (recorded in D-40, or already in earlier decisions)
- FRS §8 gives "Loss date is required." for "Must be a valid date". A serializer would reject a malformed date with its own message first, so the
  lenient converters exist to keep the FRS wording (D-40 item 3).
- FRS §14.1 puts the rejection reason in RESERVE_REJECTED's *OldValue*. Kept literally, and added to NewValue too (D-40 item 14).
- There is no FRS endpoint to add a risk object after FNOL, which made the §5.4 warning permanent for a claim created without one → Q1.
- D-24 said "5xx not stored". Storing only 2xx is simpler to explain and lets a client retry after a 409 (D-40 item 12).
- The FRS §10.4 example shows a `ClaimParties` error on create, while D-06 persists it as an issue. D-06 was followed; it was already flagged there.

## 6. What Claude got wrong (honest record)
Caught on self-review before the first run:
1. The dispatcher first called handlers with a plain `MethodInfo.Invoke`. A synchronous throw would have been wrapped in `TargetInvocationException`,
   so a 422 would have become a 500. Fixed with `BindingFlags.DoNotWrapExceptions`. `CONV_15_Before_commit_handler_exceptions_are_not_wrapped` now guards it.
2. Audit rows first defaulted `RelatedEntityId` to the claim id for claim-level events, which only duplicates `ClaimId`. It is now null unless a child is involved.
3. `AddRiskObjectCommand` first nested the input and relied on `OverridePropertyName(string.Empty)` to flatten the error keys, which was an untested
   guess about FluentValidation. Replaced with the flat-command + shared-interface pattern already used for parties.
4. The first `FailingHandler` in the atomicity test threw for *every* claim, because `ClaimCreated` does not carry the description. It now finds its
   marker through the change tracker.
5. The in-progress idempotency test hashed `body.ToJsonString()` but sent the body through `JsonContent`, assuming identical bytes. It now sends the
   exact string it hashes.

Caught by the test runs:
6. `[Produces("application/json")]` on the new controllers overrode `application/problem+json` on model-binding 422s. Caught by
   `API_16_Unknown_peril_category_returns_422`; the attributes were removed.
7. The inactive-cause-code test inserted a code that `CONV_10_Cause_of_loss_codes_match_FRS_5_6` then saw, so the result depended on test order.
   The test now deactivates a seeded code and restores it in `finally`.
8. `DomainEventDispatcher` wrapped the try/catch around all after-commit handlers of an event, so one failure skipped the rest. Caught by
   `CONV_15_After_commit_failures_are_logged_not_thrown`; the catch is now per handler.
9. Three Phase 2 persistence tests assumed a new claim has exactly one audit row. Now that creation writes its own audit trail, they find their
   row by a marker description. This was an expected consequence, not a production defect.

Caught in review of Claude's own output:
10. Two validator tests were named without a rule ID (`A_complete_intake_is_valid`, `Unknown_policy_id_is_rejected`). Renamed with `API_01_`.
11. The first matrix-update script matched rule IDs in the section 1 brief→FRS mapping table before the section 2 rows. This is the same slip as
    Phase 2 item 10. Noticed in the diff, restored from git, and redone starting at section 2.

## 7. What Vlad changed or rejected in review
- Q1 and Q2 were answered as described in §4. Nothing was rejected.
- _Further review comments to be added here. Do not invent entries._
