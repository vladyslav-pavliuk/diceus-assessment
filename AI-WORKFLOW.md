# AI workflow report

> **Status: SKELETON (Phase 8 draft).** Everything outside a `TODO (Vlad)` block is taken from the per-phase logs in
> [docs/ai-log/](docs/ai-log/) or from [docs/DECISIONS.md](docs/DECISIONS.md), and each entry cites its source. The `TODO (Vlad)` blocks are
> judgements only the author can make honestly. Claude must not fill them in.

This report follows brief §4.5.

---

## 1. AI tools used

| Tool | Used for | Source |
|---|---|---|
| Claude Code (desktop app, Code tab), model Claude Opus 5.5 | every phase: analysis (Plan mode in Phase 0), code, tests, docs, Azure infrastructure, this review | `docs/ai-log/phase-0.md` … `phase-7.md` header lines |
| The desktop app's built-in browser pane | driving the Angular app during Phase 6; this is where the date/time-picker bug (§5, item 6) was found | `docs/ai-log/phase-6.md` §5 item 1 |

> **TODO (Vlad):** add any other tools you used (e.g. Copilot or Cursor in the IDE, ChatGPT for research). Or state that Claude Code was the
> only one.

---

## 2. How the workflow was structured

### 2.1 Context loading
- **`CLAUDE.md` (in the repo) is loaded into every session.** It holds:
  - the fixed stack and the solution layout;
  - 13 non-negotiable engineering rules: CQRS naming, validation in the pipeline, the audit-before-commit / enqueue-after-commit split,
    append-only audit, event-sourced reserves, `decimal(19,4)`, `TimeProvider`, EF conventions, error mapping, Idempotency-Key;
  - a domain cheat-sheet;
  - the working agreement: phase by phase, stop and summarise, never resolve a brief/FRS conflict silently, log every correction.
- **The two specs are files in the repo**: `docs/spec/claims-frs.md` (the detailed source of truth) and `docs/spec/assessment-brief.md`. Each
  session log lists which FRS/brief sections were re-read; for example, Phase 4 loaded FRS §3, §6, §7.2, §8, §9.5–9.6, §10, §11.3, §12, §14
  (`phase-4.md` header).
- **`docs/DECISIONS.md` is the tie-breaker** when the brief and the FRS disagree. It holds D-01..D-44, each with context, options,
  recommendation, rationale and status. Claude re-reads it at the start of every phase.
- **`docs/REQUIREMENTS-MATRIX.md` is the definition of done.** Tests are named after its IDs (for example
  `BR_R_03_Self_approval_is_rejected`), so a test failure points straight at a rule.

### 2.2 Prompt sequencing
- The phase prompts are in [docs/PROMPTS.md](docs/PROMPTS.md). They follow brief §5.2:
  - 0 analysis
  - 1 skeleton + cross-cutting concerns
  - 2 domain + schema
  - 3 commands/queries/API
  - 4 reserves + Hangfire
  - 5 documents
  - 6 Angular
  - 7 Azure + CI/CD
  - 8 review + documentation
- Each prompt names the spec sections, the tests expected (with rule IDs) and a **stop point**. Some also name mid-phase checkpoints:
  - Phase 2: "show me the entity/table mapping for review" before the migration;
  - Phase 4: "write out the race conditions … before coding";
  - Phase 6: "plan … show me the plan before generating code".

### 2.3 Iteration approach
- **Phase gate.** At each stop, Claude summarised what was built, the requirement IDs covered, and the tests run with their counts. Any
  departure from written text was raised as a numbered question (Q1..Q3) with a recommended option and recorded in DECISIONS.md.
- **Decisions.** Vlad answered each question explicitly: for example "Accept Q1 and Q2, update CLAUDE.md accordingly." (`phase-4.md` §7).
  Some decisions changed direction, e.g. D-36 App Service → Container Apps (`phase-0.md` §6). Others added scope, e.g. D-40 Q1, a risk-object
  endpoint (`phase-3.md` §4).
- **Verification before "done".** `dotnet build` with warnings as errors, the full test suite (Testcontainers SQL Server + Azurite), and
  `ng lint/test/build` run at every phase. Container smoke runs came from Phase 1 onwards, and the Azure smoke test from Phase 7.
  - **Mutation checks** were used to prove that a test actually guards its rule: break the code, watch the named test fail, revert. Examples
    are the claim RowVer touch for the $10M write skew, the `Pending` predicate of the GL job, the clock-usage IL scan and the path-traversal
    containment (`phase-1.md` §6 item 9, `phase-4.md` §4, `phase-5.md` §5 item 3).
- **Honest log.** Every phase appends `docs/ai-log/phase-N.md`: the verbatim prompt, what was generated, what Vlad decided, and what Claude
  got wrong.

> **TODO (Vlad):** in your own words, describe how you prepared `CLAUDE.md` and `PROMPTS.md` before Phase 0, why you chose "phase gates +
> numbered questions" over free-form chat, and how the per-phase prompts changed as you learned what Claude does well or badly.

---

## 3. Representative prompts

All verbatim, from `docs/PROMPTS.md` / `docs/ai-log/`.

**Prompt 1: Phase 0 kickoff, analysis only** (`phase-0.md` §1, excerpt).
*Purpose:* load the full specs, and make Claude surface every conflict before writing any code. Several suspected conflicts were seeded
(D-01..D-18) for Claude to *verify* rather than accept.

```
PHASE 0 — ANALYSIS ONLY. Do not write application code yet.
...
2. docs/DECISIONS.md
   Every conflict, gap, or ambiguity between the two documents. For each one: context, options, your
   recommendation, and a rationale. Mark each as PROPOSED. I will accept or change them. At minimum, analyse these
   items I already found (verify them yourself and look for more):
   D-01 SLA job: the brief says "flag with SlaBreached status", while the FRS says "do not change claim status".
...
Rules for this phase:
- Quote the section number for every claim you make about the spec. If the spec does not say something, write
  "NOT SPECIFIED" rather than inventing a requirement.
```
*Result:* 37 decisions. 19 of them (D-19..D-37) were new findings. Claude also pushed back on three of the prompt's own suggestions
(`phase-0.md` §4): D-01, D-06 and D-16.

**Prompt 2: Phase 4, concurrency before code** (`phase-4.md` §1, excerpt).
*Purpose:* the area brief §5.2 calls "a good area to test AI reasoning on concurrency constraints". The prompt forces a written race analysis
before any code, and asks for tests of the concurrent cases, not just the sequential ones.

```
Phase 4: reserves and background jobs (FRS §6, §7.2, §12; brief §3.3.3, §3.5). This is the area reviewers
probe hardest, so reason explicitly about concurrency before writing code.
...
- GL posting: the after-commit enqueue; PostGLReserveChangeJob idempotency via a conditional update + a single
  audit write in one transaction; retries; the Failed state + GL_POSTING_FAILED after retries are exhausted.
  Tests: running the job twice produces exactly one GL_POSTING_SIMULATED; running it concurrently twice also
  produces exactly one; the failure path.
...
Before coding, write out the race conditions you are defending against and how each is handled. That text goes
into ARCHITECTURE-PLAN.md. Stop and summarise.
```
*Result:*
- ARCHITECTURE-PLAN §6.1, which lists 13 races (R1–R13) with their defences and proving tests.
- Claude questioned the rule in `CLAUDE.md` itself: `<> 'Posted'` → `= 'Pending'` (D-41 Q1, accepted, and CLAUDE.md updated).

**Prompt 3: Phase 2, review checkpoint mid-phase** (`phase-2.md` §1, excerpt).
*Purpose:* stop the generator before the most expensive-to-change artefact (the migration) and verify the conventions by eye.

```
Before generating the migration, show me the entity/table mapping for review. Afterwards, show me the generated
migration's column types for Claims, ReserveHistory, and ClaimAuditLog so I can verify the conventions.
```
*Result:* two questions answered by Vlad ("let's keep guid"; "approved"). The review of the generated DDL also caught `RowVer rowversion NULL`
against FRS §15.1's NOT NULL (`phase-2.md` §6 item 3).

**Prompt 4: a direction change** (`phase-0.md` §6, verbatim).

```
Use azure container apps to reduce db costs.
Apply the most logical and optimal options for the decisions, I'll adjust the functionality or business rules after we build and deploy the first version of the app.
```
*Result:* D-36 was rewritten: Container Apps with scale-to-zero, serverless SQL with auto-pause, and the documented consequences (cold start,
SLA runs only while a replica is up, the `minReplicas 1` runbook step).

> **TODO (Vlad):** pick the 3–4 you want to present live, and say *why* each is representative of how you prompt.

---

## 4. What was AI-generated and what was designed or refined by hand

**Documented facts (from the logs):**
- Almost all code, tests and docs were generated by Claude inside the phase gates above.
- These decisions were Vlad's, not Claude's recommendation:
  - D-36: Container Apps instead of App Service (`phase-0.md` §6);
  - D-40 Q1: add `POST /claims/{id}/risk-objects` (`phase-3.md` §4);
  - D-39 Q1: `CorrelationId` stays a GUID (`phase-2.md` §4);
  - the D-37 CVE handling, chosen from three options (`phase-1.md` §4);
  - Phase 6: dropping the per-screen stops (`phase-6.md` §1).
- These were Claude's recommendations, explicitly accepted by Vlad: D-01..D-35 and D-37 (`phase-0.md` §6), D-41 Q1–Q2, and D-42..D-44
  Q1–Q3.
- Phase 8: Vlad chose which review findings to fix (F1–F3) and approved D-45 (`phase-8.md` §7).
- The numbered items of D-38 and D-40..D-44 are still **PROPOSED** in DECISIONS.md (the per-decision questions are accepted; the items are not).
- **Recorded review comments from Vlad: none yet.** Every "What Vlad changed or rejected in review" section in `docs/ai-log/` is still a
  placeholder.

> **TODO (Vlad):** this is the section reviewers weigh most. Write, honestly:
> - which parts you designed yourself before prompting (e.g. the phase plan, the CLAUDE.md rules, the seeded suspicions in the kickoff prompt);
> - which generated parts you reviewed line by line, and which you accepted on the strength of tests;
> - anything you rewrote by hand;
> - the review comments you gave that are not yet in the logs. Add them to the matching `docs/ai-log/phase-N.md` §7 first, so this section can
>   cite them.

---

## 5. Where AI output was wrong or suboptimal, and how it was corrected

Every item below is recorded in `docs/ai-log/`. Most were caught by Claude's own tests, builds or re-reads, or by the first real Azure run.
**None came from a review comment by Vlad** (see §4).

| # | Phase | What Claude got wrong | How it was caught | Fix | Source |
|---|---|---|---|---|---|
| 1 | 2 | Every key had a `NEWSEQUENTIALID()` default, so EF treated keys as store-generated. A new child added to a loaded claim (a party, a reserve transaction) would have been sent as an **UPDATE** of a row that does not exist | `ImmutableRowsInterceptor` threw in the first integration run | `ValueGeneratedNever()` with the SQL default kept; migration regenerated | `phase-2.md` §6 item 1 |
| 2 | 3 | The domain-event dispatcher used plain `MethodInfo.Invoke`, so a 422 thrown by a handler would have surfaced as a **500** (wrapped in `TargetInvocationException`) | Claude's own re-read | `BindingFlags.DoNotWrapExceptions`, plus a guard test | `phase-3.md` §6 item 1 |
| 3 | 3→5 | The Idempotency-Key filter hashed the raw body. A multipart boundary is random, so a genuine retry of an upload was refused as a "different request". A Phase 3 defect | a Phase 5 test (`API_IDEMP_A_repeated_upload_with_the_same_key_stores_one_document`) | forms hashed by content (D-42 item 18) | `phase-5.md` §5 item 1 |
| 4 | 5 | D-42 claimed an oversized body "is cut off with 413". On real Kestrel it came back as a **422** with framework wording | container smoke run | `RejectBodiesLargerThanAttribute` answers 413 from Content-Length, with a test | `phase-5.md` §5 item 2 |
| 5 | 4 | A race test (`RSV_07_Retract_racing_an_approval…`) passed **by luck**: the interleaving it claimed to test was not forced | Claude's re-read | `ConcurrencyGate.FirstArrival` forces the interleaving on every run | `phase-4.md` §5 item 2 |
| 6 | 6 | The date and time pickers shared one FormControl, so a loss date of 1 Sep 14:30 became *today* 14:30 | only by driving the form in the browser; the unit tests could not see it | a separate `lossTime` control, with a test | `phase-6.md` §5 item 1 |
| 7 | 6 | **An invented rule:** a 4,000-character limit on claim notes. The column is NVARCHAR(MAX) and the API has no limit | checking the configuration | removed | `phase-6.md` §5 item 2 |
| 8 | 7 | `bootstrap.sh` hard-coded the name-only OIDC subject. This repository issues immutable subjects, so Azure sign-in failed (`AADSTS700213`) | the first Azure run | read `sub_claim_prefix` from the GitHub API | `phase-7.md` §7 item 2 |
| 9 | 7 | The Storage Blob Delegator role id was **written from memory, wrong** (`RoleDefinitionDoesNotExist`) | the Azure run; the Bicep linter cannot catch it | all four role ids looked up with `az role definition list`. Lesson: built-in ids are looked up, never recalled | `phase-7.md` §7 item 3 |
| 10 | 7 | The migrations step used `efbundle --connection`. The bundle reads `ConnectionStrings:ClaimsDb` while it builds the service provider, before `--connection` applies. Built locally, but never *run* the way the workflow ran it | the Azure run | pass `ConnectionStrings__ClaimsDb` as an environment variable | `phase-7.md` §7 item 4 |
| 11 | 1 | Copied the claim that AutoMapper 14 is "Apache-licensed" from CLAUDE.md into D-37 without checking. It is MIT | checking nuget.org | CLAUDE.md and D-37 corrected | `phase-1.md` §5 |
| 12 | 0 | Invented a rule ID (`BR-P-03`) that is not in the FRS | Claude's re-read | renamed `PTY-01` | `phase-0.md` §5 |

**Where Claude corrected the human-written context** (for balance, and because it shows the context was challenged rather than obeyed):
- `CLAUDE.md`'s GL rule `<> 'Posted'` let a stray job post a Failed row behind the user's audited retry. It became `= 'Pending'` (D-41 Q1).
- The prompt's suggested SLA flag column would have bumped `UpdatedAt` and reset the 48-hour clock it measures (D-01, `phase-0.md` §4).
- One seeded manager could never have approved their own >$100k transaction, so there are two per role (D-16).

**Found in the Phase 8 review of our own code** (`docs/ai-log/phase-8.md` §4). Vlad approved the fixes: "Apply F1, F2 and F3, then merge to main".
- F1: FRS §5.4's "Policy not found" warning had not been reconciled with the 422 for an unknown `policyId`. D-40 item 6 was amended; no code
  change.
- F2: a submitter could *reject* their own pending reserve. It is now refused, and the submitter retracts instead (D-45, mutation-checked test).
- F3: the Add Reserve authority preview ignored the $10M escalation, so it showed "Auto-approved" where the API would require a manager. Fixed,
  with tests.

F2 and F3 were Claude's own gaps from Phases 4 and 6, found only when the finished code was read again as a reviewer would.

> **TODO (Vlad):**
> - Choose the **two or more** examples you will walk through live. Item 1 (keys) and item 5 (a test that passed by luck) show judgement
>   about *why* the output was wrong. Items 8–10 show the limits of local verification.
> - Add any correction **you** made that is not in the logs. The brief values those most.

---

## 6. Honest assessment: where AI helped most, and least

> **TODO (Vlad):** your judgement. Facts from the logs you may want to weigh:
> - The spec analysis in Phase 0 produced 37 decisions, 19 of them new findings.
> - There are 695 backend and 100 frontend tests, named after the rule IDs, including forced-interleaving concurrency tests and mutation checks.
> - All three Azure defects (§5 items 8–10) passed every local check and failed only against real Azure.
> - The Phase 6 date-picker bug was invisible to the unit tests and found only in the browser.
> - Where did AI save the most time? Where did reviewing its output cost more than writing it yourself would have?

---

## 7. AI interaction history (brief §4.6)

- Per-phase logs with the verbatim prompts, the decisions and the corrections: [docs/ai-log/](docs/ai-log/) (phases 0–8).
- The prompt set: [docs/PROMPTS.md](docs/PROMPTS.md). The session context file: [CLAUDE.md](CLAUDE.md).

> **TODO (Vlad):** export the Claude Code sessions (`/export`, or the desktop app's transcript export), commit them under
> `docs/ai-log/exports/`, and link them here. The requirements matrix marks this deliverable (DEL-04) as **Missing** until then.
