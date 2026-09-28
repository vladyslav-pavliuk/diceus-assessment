# AI log: Phase 4 (reserves, authority, Hangfire GL posting and SLA jobs)

- **Date:** 2026-09-28 (one session).
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:** `CLAUDE.md`, `docs/DECISIONS.md` (in full), `docs/PROMPTS.md` Phase 4, `docs/ARCHITECTURE-PLAN.md`, `docs/REQUIREMENTS-MATRIX.md`,
  `docs/ai-log/phase-3.md`, FRS §3, §6, §7.2, §8, §9.5–9.6, §10, §11.3 Tab 3, §12, §14; brief §3.3.3, §3.4.2, §3.5; the Phase 2–3 domain, application,
  persistence and API code, and the test fixtures.

## 1. Prompt given (verbatim)

The Phase 4 prompt from `docs/PROMPTS.md`:
```
Phase 4: reserves and background jobs (FRS §6, §7.2, §12; brief §3.3.3, §3.5). This is the area reviewers
probe hardest, so reason explicitly about concurrency before writing code.
- Submit (open/adjust), Approve, Reject, Retract, SetReserveLimitOverride (Manager), RetryGlPosting.
  Enforce the backend authority checks in the domain and the handler, not just via [Authorize]. Self-approval →
  422 with the exact message. Approving above the caller's authority → 422/403 per DECISIONS.
- Concurrency: two approvers approving the same txn at once, or two adjustments to the same component at once →
  exactly one succeeds (RowVer → 409). Write tests for both.
- GL posting: the after-commit enqueue; PostGLReserveChangeJob idempotency via a conditional update + a single
  audit write in one transaction; retries; the Failed state + GL_POSTING_FAILED after retries are exhausted.
  Tests: running the job twice produces exactly one GL_POSTING_SIMULATED; running it concurrently twice also
  produces exactly one; the failure path.
- SlaMonitoringJob as a recurring job registered at startup, */15; the 48h rule; ≤1 breach event per claim per
  24h; no status change. Tests use a fake TimeProvider.
- Hangfire dashboard behind the Manager role (or dev-only).
Before coding, write out the race conditions you are defending against and how each is handled. That text goes
into ARCHITECTURE-PLAN.md. Stop and summarise.
```
No other instructions were given during the session.

## 2. Requirement IDs touched
BR-R-01/02/03/05/06, BR-C-06 (reserve endpoint), BR-A-02; VAL-08/09/10/13/14; RSV-02/03/05/07; API-10..15, API-24, API-25, API-IDEMP (clean-up);
JOB-01..12, new JOB-13; AUD-05..11, AUD-15, AUD-17; SEC-02; new OPS-03; CONV-14.
Decisions applied: D-01, D-04, D-05, D-08, D-11, D-15, D-21, D-22, D-23, D-24, D-25, D-26, D-31, D-35, D-36; new **D-41** (Q1 and Q2 open, 19 items PROPOSED).

## 3. What was generated

| Area | Content |
|---|---|
| Plan (first, before code) | ARCHITECTURE-PLAN §6 revised; new **§6.1 "Race conditions we defend against"**: four mechanisms (M1 claim/component RowVer, M2 unique indexes → 409, M3 compare-and-set, M4 after-commit + sweeper) and 13 races (R1–R13), each with the failure it prevents, the defence and the test that proves it |
| Domain | `Claim.RetryGlPosting` + `GlPostingRetryRequested` event; `ReserveTransaction.RequeuePosting` (Failed → Pending) and a documented posting lifecycle; `GlJournalEntry` (sign-aware DR/CR); `SlaPolicy` (48h strict, 24h inclusive, verbatim description) |
| Application | 7 reserve commands (Submit, Adjust alias, Approve, Reject, Retract, RetryGlPosting, SetReserveLimitOverride) with validators; `GetClaimReservesQuery`; 4 job commands (`PostGlReserveChange`, `MarkGlPostingFailed`, `DetectSlaBreaches`, `RequeueStrandedGlPostings`); `GlPostingEnqueuer` (after-commit handler); abstractions `IGlPostingStore`, `IGeneralLedger`, `ITenantDirectory`, `ISlaMonitoringQueries`; shared `AuditValues`; `InitialReserveDto` renamed `ReserveSubmittedDto` (same JSON) |
| Persistence | `GlPostingStore` (compare-and-set `ExecuteUpdate`s), `TenantDirectory` (the only cross-tenant reads, D-31), `SlaMonitoringQueries`, `ClaimQueries.GetReservesAsync` (3 queries), `IdempotencyStore.PurgeAsync`; `PostingStatus` concurrency token; unique filtered index on GL_POSTING_SIMULATED per transaction (migration `AddGlPostingAuditBackstop`); duplicate key at commit → `ConflictException` (409) |
| Infrastructure | Hangfire 1.8.25 (SQL storage in the app database, DI-registered `JobStorage`, `Jobs:RunServer`); `PostGLReserveChangeJob` (3 retries at 10/30/60 s, last attempt → Failed + audit + rethrow), `SlaMonitoringJob` (`*/15`, `DisableConcurrentExecution`), `GlPostingSweeperJob` (`*/5`), `IdempotencyCleanupJob` (daily); `JobScopes` (tenant + correlation per step); `SimulatedGeneralLedger` (`Jobs:GlPosting:SimulateFailure`); recurring registration as a hosted service |
| API | `ReservesController` (6 endpoints), `PUT /claims/{id}/reserve-limit-override`; Hangfire dashboard at `/hangfire`, Manager policy, read-only, with the `?access_token=` → scoped cookie hand-off (`DashboardTokenHandoff`); 409 title generalised |
| Tests | Domain 269 (+5), Application 51 (+16), Integration 228 (+51): reserve endpoints (21), concurrency (4), GL job (14, including a real `BackgroundJobServer` end to end), SLA job (7), registration and dashboard (3), retry endpoint (2). New fixtures: `ConcurrencyGate` (forces "both loaded before either writes"), `ControllableLedger` (fail / hold a posting open), `JobsHost` (second host, own database, `FakeTimeProvider`), `SqlDiagnostics.WaitForBlockedRequestAsync`, Hangfire storage readers |
| Docs | D-41 (+ D-37 package rows), ARCHITECTURE-PLAN §6/§6.1, matrix (44 rows now Implemented P4, new JOB-13 and OPS-03), REVIEW-PREP Phase 4, this log |

## 4. Verification that ran
- Host (SDK 10, `DOTNET_ROLL_FORWARD=Major`): `dotnet build` 0 warnings; `dotnet test` **548/548** (269 domain, 51 application, 228 integration).
  The concurrency, GL, SLA and retry tests (27) were then run 5 more times: 5/5 green.
- `mcr.microsoft.com/dotnet/sdk:9.0` (SDK 9.0.318), Testcontainers SQL Server 2022 through the host Docker socket: build with `-warnaserror`, 0 warnings,
  **548/548**.
- **Mutation checks** (each reverted afterwards):
  - Dropping `PostingStatus = 'Pending'` from the GL compare-and-set made 4 tests fail: run twice, run concurrently, stray job after Failed, failure after posted.
  - Commenting out the claim "touch" in `AuditColumnsInterceptor` made exactly `BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit` fail (both
    $6M approvals returned 200: write skew), while the same-component races still passed on the component RowVer, as §6.1 predicts.
  - Disabling the duplicate-key → 409 mapping made `BR_R_06_Database_rejects_a_second_gl_posting_entry` fail, but **not** the same-component race
    test (3 runs). See §5 item 4.
- **Container smoke** (`docker compose --profile app`, migrator + API with an in-process Hangfire server), against a fresh database name through a
  temporary override file in the scratchpad (the existing `sqlserver-data` volume holds a Phase 1 database, see §5 item 5):
  - FNOL with a $5,000 initial reserve → AutoApproved → Posted by Hangfire (job 1) within 3 s; a $50,000 reserve → handler approve **403** → supervisor
    approve **200** → Posted (job 2); audit: RESERVE_CREATED, RESERVE_AUTO_APPROVED, RESERVE_APPROVED, 2 × GL_POSTING_SIMULATED with the FRS journal text.
  - Dashboard: supervisor token **403**, manager **200**; the three recurring jobs listed.
  - `Jobs__GlPosting__SimulateFailure=true`: a $1,200 reserve → after about 2 minutes `Failed`, GL_POSTING_FAILED "failed after 4 attempts:
    GeneralLedgerUnavailableException: …", job shown under Failed in the dashboard. Restarted without the flag → `retry-posting` **202** →
    GL_POSTING_RETRIED (by Blake Jordan) → Posted by a new job. The ledger log line carried the job's correlation id.
  - Stack stopped afterwards (`down`, volumes kept).

## 5. What Claude got wrong, and what changed

No correction came from Vlad in this session. These are Claude's own mistakes, found by the build, the tests or a re-read, and fixed before the summary:

1. **Test ledger bug made the concurrency test hang.** In `ControllableLedger`, entering the "hold" cleared the release handle, so
   `ReleaseHeldPosting()` released nothing; run A waited 30 s and run B's UPDATE hit the SQL command timeout. The first run of
   `BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry` failed with `Execution Timeout Expired`. Fixed by keeping the held handle separately.
2. **A racy race test.** The first version of `RSV_07_Retract_racing_an_approval_leaves_one_outcome` started the approval and immediately retracted; if
   the retraction committed before the approval loaded the claim, the approval would get 422 instead of 409. It passed, but by luck. Fixed with
   `ConcurrencyGate.FirstArrival`, so the retraction starts only after the approval has loaded the claim.
3. **Namespace dependency slip.** The first `ReserveTransaction.RequeuePosting` threw a `BusinessRuleViolationException` with `Claims.ErrorKeys`, making
   `Domain.Reserves` depend on `Domain.Claims` for the first time. Moved the rule check into the aggregate (as `Approve` already does) and left an
   `InvalidOperationException` guard in the entity.
4. **An analysis claim the mutation check did not confirm.** §6.1 R2 says the losing same-component submission fails on "whichever statement runs
   first", RowVer or duplicate key. With the mapping disabled, the race test still passed in 3 runs: EF Core issues the UPDATEs (component, claim) before
   the INSERT, so the RowVer always trips first. The mapping is kept (it covers the backstop index, the other unique indexes and a future ordering
   change) and is exercised by `BR_R_06_Database_rejects_a_second_gl_posting_entry`; D-41 item 5 records the observation.
5. **Compose smoke first failed on stale local data.** The migrator stopped with "There is already an object named 'Organisations'": the persistent
   `sqlserver-data` volume holds the Phase 1 `20260927175518_InitialCreate`, which Phase 2 regenerated. Not a Phase 4 defect. Claude did not delete the
   volume; the smoke ran on a new database name instead. Vlad may want `docker compose down -v` once.
6. Two compile errors on the first test build (`with {}` on a class; a missing `Hangfire.Common` using), fixed immediately.

Where Claude deliberately departed from written text, it did not decide silently: D-41 **Q1** (`= 'Pending'` instead of CLAUDE.md's `<> 'Posted'`) and
**Q2** (constant retry count; rethrow after recording the failure, amending D-35) are open questions for Vlad, implemented as recommended.

## 6. Open items for Vlad
- ~~Decide D-41 Q1 and Q2.~~ Done, see §7.
- Review D-41 items 1–19 (PROPOSED), and D-38 / D-40 items, still PROPOSED.
- Phase 7: Hangfire creates its schema at first start, so the app's database user needs DDL rights then (D-41 item 14); the startup registration
  needs the database reachable, so a paused serverless database delays the first start (EF retries do not cover Hangfire's own connection).

## 7. Vlad's decisions after the summary
Vlad's reply (verbatim):
```
Accept Q1 and Q2, update CLAUDE.md accordingly.
```
Applied: the GL-job line in `CLAUDE.md` now specifies `PostingStatus='Pending'` (plus approved and all three job arguments matching), the two
backstop indexes, the constant retry count and the rethrow after recording the failure. D-41 Q1/Q2 marked ACCEPTED, D-35 amended, REVIEW-PREP and
the matrix updated. No code change was needed: both were already implemented as recommended.

