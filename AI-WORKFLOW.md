# AI workflow report

> **How this report was written.** Phase 8 drafted it from the per-phase logs in [docs/ai-log/](docs/ai-log/) and from
> [docs/DECISIONS.md](docs/DECISIONS.md), and left `TODO (Vlad)` blocks for the author's judgement. In the refinement phase Vlad asked Claude to
> complete those blocks. They are written from the logs, the git history and the refinement conversation
> ([phase-refinement.md](docs/ai-log/phase-refinement.md)), each claim cites its source, and nothing is stated that the record does not show. Vlad owns
> the final wording.

This report follows brief §4.5.

---

## 1. AI tools used

| Tool | Used for | Source |
|---|---|---|
| Claude Code (desktop app, Code tab), model Claude Opus 5.5 | every phase: analysis (Plan mode in Phase 0), code, tests, docs, Azure infrastructure, this review | `docs/ai-log/phase-0.md` … `phase-7.md` header lines |
| The desktop app's built-in browser pane | driving the Angular app during Phase 6, where the date/time-picker bug (§5, item 6) was found; measuring the Link policy spacing bug in the refinement phase (§5.1, V3) | `docs/ai-log/phase-6.md` §5 item 1; `phase-refinement.md` §5 |

Claude Code is the only AI tool recorded in the logs and in the git history: every non-merge commit carries the Claude co-author line. Git, the
GitHub CLI, Docker, the .NET and Angular CLIs and the Azure CLI were used as ordinary tooling, driven through Claude Code.

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

### 2.4 How the context was prepared, and how the prompts changed
- **Prepared before Phase 0.** The first commit (`cdc361d`) holds `CLAUDE.md` (127 lines), `docs/PROMPTS.md` (286 lines) and both specs.
  The prompt pack already contained:
  - the phase plan with its stop points;
  - 18 suspected brief/FRS conflicts (D-01..D-18) that Claude had to *verify* rather than accept;
  - a note on collecting evidence for this report from the start ("Every time you reject or fix Claude's output, one line in
    `docs/ai-log/phase-N.md` saves you work later").
- **Why phase gates and numbered questions rather than free-form chat.** `CLAUDE.md` states the constraint: reviewers weigh "correctness,
  consistency and explainable decisions", and the author "must be able to defend every line in a 90-minute live review". A gate produces a
  diff small enough to review, a test count, and a written decision (D-xx) for every departure from the specs. Free-form chat would have
  left those decisions implicit.
- **How the prompts changed.**
  - *Phase 0.* After the analysis, Vlad delegated the remaining choices ("Apply the most logical and optimal options for the decisions…").
  - *Phases 1–7.* The spec-heavy prompts stayed. The context file was updated whenever a decision changed a rule: the D-36 stack line, and the
    D-41 GL predicate ("Accept Q1 and Q2, update CLAUDE.md accordingly").
  - *Phase 6.* After approving the plan, Vlad dropped the per-screen stops: "don't stop after each screen, I'll check everything at once and
    refine if needed" (`phase-6.md` §1). That review happened in the refinement phase (V3, V4 below).
  - *Phase 8.* A review-only prompt: Claude acted as a strict reviewer of its own output.
  - *Refinement.* Prompts became short review comments on finished code: noisy comments, a class name, a suspected dead class, inline
    templates, a screenshot. Specs were no longer needed; the corrections were about quality and consistency (`phase-refinement.md` §1).

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

**Prompt 5: refinement, reviewing finished output** (`phase-refinement.md` §1, verbatim).
*Purpose:* correct quality problems that no test catches. It asks a question instead of issuing an order ("any reasons for that?"), and it
puts a guard on the clean-up ("be careful if they are unused for purpose like validator classes").

```
We need to refactor the solution.
Let's start from the .NET backend.

* Remove comments in places where code is self-explained and make <summary> blocks shorter to point on significant details only.
* ApiProblems class name is inappropriate for an enterprise project.
* ClaimAuditTrail is unused, any reasons for that? + Check fir any other unused imports, classes, properties etc., but be careful if they are unused for purpose like validator classes.
```
*Result:* backend comment lines went from 1,739 to 823, and frontend ones from about 450 to 219. A script compared both code versions with the
comments stripped and proved that nothing else changed. `ApiProblems` became `ErrorResponseFactory`. `ClaimAuditTrail` turned out not to be
dead: it is registered by an assembly scan, which its summary now says. The unused-code scan found a package reference unused since Phase 1.

**Chosen for the live walkthrough**, and why each is representative:
1. **Prompt 1 (Phase 0 kickoff).** Context loading done deliberately: the full specs, the rules file, and suspicions seeded for Claude to test,
   not accept. Claude then pushed back on three of the prompt's own suggestions.
2. **Prompt 2 (Phase 4, concurrency before code).** In the riskiest area, the prompt demands written reasoning and concurrent tests *before*
   code. That is how the `= 'Pending'` correction to the human-written `CLAUDE.md` came about.
3. **Prompt 5 (refinement).** Reviewing generated output rather than generating more: naming, noise, a question, and a guard against an
   over-eager clean-up.
4. *(If time allows)* **Prompt 4 (the direction change).** Two sentences that overrode Claude's hosting recommendation on cost.

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
- **Review comments from Vlad** on finished code are recorded in the refinement phase (`phase-refinement.md` §8, and §5.1 below). The §7 sections
  of phases 1–7 record decisions only, no line-level review comments.

**Designed by Vlad before prompting** (git history, `cdc361d`):
- the phase plan and each phase's stop points (`docs/PROMPTS.md`);
- the 13 engineering rules and the domain cheat-sheet (`CLAUDE.md`);
- the 18 seeded conflict suspicions of the kickoff prompt;
- the decision to keep an honest per-phase log from the start.

**Reviewed by eye** (from the logs):
- the entity/table mapping and the generated migration DDL (Phase 2, which caught the nullable RowVer);
- the Phase 6 plan;
- the Phase 8 findings, of which Vlad chose F1–F3;
- in the refinement phase, the finished code and the running UI. That review found the verbose comments, the `ApiProblems` name, the
  suspected-unused `ClaimAuditTrail`, the inline templates and the button-spacing bug (§5.1).

**Accepted on the strength of tests and summaries:** the bulk of Phases 1–7. The logs record no line-level comments for those phases. Acceptance
rested on the phase summaries, the rule-named tests (695 backend, 100 frontend), the mutation checks and the smoke runs.

**Written by hand:** none in git. Every non-merge commit is co-authored by Claude; every change, including the refinement corrections, was
made by directing Claude rather than by editing files directly.

---

## 5. Where AI output was wrong or suboptimal, and how it was corrected

Every item is recorded in `docs/ai-log/`. The first table lists what Claude's own tests, builds or re-reads caught, or the first real Azure run.
**§5.1 lists what Vlad caught**: those are the corrections the automated checks could not make.

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

### 5.1 Corrections that came from Vlad, most valuable first

| # | What Claude produced | Vlad's correction | Outcome | Source |
|---|---|---|---|---|
| V1 | **Over-documented code across all phases.** 1,739 backend comment lines out of 11,687, with summaries that restated member names, routes and audit events, plus references to internal files a product codebase should not carry (`CLAUDE.md rule 5`, `ARCHITECTURE-PLAN §6.1 R6`, `Phase 4`, even "decided by Vlad 2026-09-28") | "Remove comments in places where code is self-explained … point on significant details only", for the backend and then the frontend | Backend 1,739 → 823 lines, frontend about 450 → 219. Kept: the *why* (races, transaction order, security) and rule/decision IDs. A comment-stripping comparison proved that no code changed | `phase-refinement.md` §1–§3 |
| V2 | **A hosting recommendation that put a warm demo over cost.** App Service B1 Always On + Azure SQL Basic | "Use azure container apps to reduce db costs" | D-36 rewritten: Container Apps with scale-to-zero and serverless SQL with auto-pause; the cold-start consequences documented and a `minReplicas` input added to deploy | `phase-0.md` §6 |
| V3 | **A visible UI bug shipped since Phase 6.** The Link policy button touched the field above it (the field's hint area collapses to 0 px, and the component host is inline). It passed 100 tests, lint, the Phase 8 review and the first refinement pass | A screenshot: "please add top margin for the button" | Measured in the browser, fixed on the shared `.tab__actions` rule, which also fixed the Save notes row | `phase-refinement.md` §5 |
| V4 | **Inconsistent conventions.** 13 of 29 components had inline templates (9 also inline styles) | "I prefer separate files for that" | All 13 moved verbatim to `.html`/`.scss` | `phase-refinement.md` §3 |
| V5 | **A weak name.** `ApiProblems` (Phase 1) for the error-response builder | "inappropriate for an enterprise project" | `ErrorResponseFactory`. Claude avoided `ProblemDetailsFactory`, which would shadow ASP.NET Core's class | `phase-refinement.md` §2 |
| V6 | **Unused leftovers the Phase 8 review missed**, and an undocumented registration | "ClaimAuditTrail is unused, any reasons for that? + Check for any other unused …" | `ClaimAuditTrail` was not dead (assembly-scanned), and its summary now says so. The resulting scan removed a package reference unused since Phase 1, an unread test field and 6 unused `using`s | `phase-refinement.md` §2 |
| V7 | **A "fixed" report without saying where.** After V3, Claude verified the fix only on the local dev server and did not say the Azure site needs a manual deploy | "I still don't see fixed button problem" | The deployed bundle was checked and still held the old CSS rule. Lesson: say where a fix is verified and where it is not yet live | `phase-refinement.md` §5 |

**Chosen for the live walkthrough:**
- **Item 1 (keys) and item 5 (a test that passed by luck):** judgement about *why* output that looked right was wrong.
- **Items 8–10 (Azure):** the limits of local verification.
- **V3 and V7 (the button):** what only a human looking at the screen caught, and how "done" has to name *where* it is done.
- **V1 (comments):** Claude's systematic bias towards volume, and how the clean-up was proven safe.

---

## 6. Honest assessment: where AI helped most, and least

**Where AI helped most:**
- **Reading and reconciling the specs.** Phase 0 turned two long documents into 37 decisions, 19 of them new findings. Claude also pushed back
  on three of the prompt's own suggestions (D-01, D-06, D-16).
- **Reasoning about concurrency, and proving it.** It listed 13 races (R1–R13) and gave each a defence and a test, with forced interleavings and
  mutation checks. It also corrected the human-written GL rule in `CLAUDE.md` (`<> 'Posted'` → `= 'Pending'`).
- **Consistent volume under explicit rules.** The whole slice was built in two days (27–28 Sep, per git history): 695 backend and 100 frontend
  tests named after rule IDs, and conventions enforced by architecture tests.
- **Large mechanical changes that can be verified.** In the refinement phase: a comment clean-up proven safe by a code-equivalence script,
  255 + 40 documentation line references remapped, and branches merged and pruned.

**Where it helped least, or where reviewing cost more than it saved:**
- **Anything outside the test harness.** All three Azure defects (§5 items 8–10) passed every local check. The date-picker bug (item 6) and the
  button spacing (V3) were invisible to the unit tests; only a browser or a human eye found them.
- **A bias towards volume.** Claude documents everything, including what the code already says and which internal file a rule came from. It
  took a dedicated refinement pass (V1) to bring the code back to a level a reviewer can read.
- **Drift between phases.** Conventions set early were not always kept later (inline vs external templates, V4). Leftovers survived a
  self-review (V6).
- **Facts from memory.** A licence (item 11), a role id (item 9) and a rule ID (item 12) were stated confidently and were wrong. Anything
  checkable is now looked up, not recalled.
- **"Done" without scope.** A fix verified locally was reported as fixed without saying it was not deployed (V7).

**Rules taken forward:**
- Prompt for reasoning before code in risky areas.
- Name tests after rule IDs, and use mutation checks to prove a test guards its rule.
- Look at the running UI, not just the tests.
- Verify identifiers and licences against their source.
- Ask for "why" comments only.
- When reporting a fix, say where it has been verified.

---

## 7. AI interaction history (brief §4.6)

- Per-phase logs with the verbatim prompts, the decisions and the corrections: [docs/ai-log/](docs/ai-log/) (phases 0–8 and the refinement phase).
- The prompt set: [docs/PROMPTS.md](docs/PROMPTS.md). The session context file: [CLAUDE.md](CLAUDE.md).

- The refinement phase: [docs/ai-log/phase-refinement.md](docs/ai-log/phase-refinement.md).
- **Session exports (DEL-04), still open.** The project ran in 11 Claude Code sessions: Phase 0 analysis, Phases 1–8, one branch-merge session, and
  the refinement session. The raw transcripts are not yet committed. The desktop app exports them as zip files, at most six per hour, and they
  include full command output (for example from the Azure sessions). They should be checked for secrets before they are committed under
  `docs/ai-log/exports/`.
