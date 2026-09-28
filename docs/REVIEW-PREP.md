# Review prep for the live technical review (brief §7)

This file has three parts:
- **Part 1:** 25 likely deep-dive questions with short answers and code references. The references were checked against the code in
  Phase 8.
- **Part 2:** five rehearsal tasks for the live-implementation slot (brief §7.2 item 11), with the files to touch.
- **Appendix:** the phase-by-phase question bank written during Phases 0–7, for depth on any topic.

Paths are relative to the repository root. `Dom`/`App`/`Int`/`Web` mark the test project a test lives in (see the requirements matrix).

---

## Part 1: 25 deep-dive questions

### Architecture and CQRS

**1. Walk me through the layers. How do you know the dependency rule holds?**
- The layers are Domain ← Application ← (Infrastructure, Persistence) ← API.
- **Project references** make an illegal reference fail to compile.
- **NetArchTest** catches leaks through transitive references: Application may not touch EF Core, SqlClient, ASP.NET Core, Hangfire or Azure
  types, and controllers may not touch Persistence (`tests/ClaimsModule.IntegrationTests/Architecture/LayerDependencyTests.cs:13-76`).
- **An IL scan** forbids `DateTime.Now`/`UtcNow` and `DateTimeOffset.Now`/`UtcNow` in production code (`ClockUsageTests`, CONV-16). It was
  mutation-checked: adding a clock read made it fail, naming the method.

**2. Why is "approve reserve" a Command rather than a direct service call?**
- It changes state, so it goes through the write pipeline:
  - `LoggingBehavior` → `ValidationBehavior` (422 before any transaction) → `UnitOfWorkBehavior` (one transaction, audit before commit,
    enqueue after commit). The order is set in `src/ClaimsModule.Application/DependencyInjection.cs:21-23`.
  - Only commands get a unit of work; queries skip it (`src/ClaimsModule.Application/Common/Behaviors/UnitOfWorkBehavior.cs:20`).
- A service call would have to repeat those concerns, or skip them.
- The command is also a named, testable unit that maps 1:1 to a business action and a matrix row.
- The controller only binds and sends: `src/ClaimsModule.API/Controllers/ReservesController.cs:60-68`.

**3. What would break if you removed the Unit of Work?** (brief §7.4)

`src/ClaimsModule.Persistence/UnitOfWork.cs:35-62` runs, in one transaction: the handler → before-commit events (audit rows) → `SaveChanges`
→ `COMMIT`. After-commit events run outside the replayed block. Without it:
- **Audit and state could diverge:** state with no audit row, or an audit row for a change that never happened.
- **Claim numbers could have gaps:** the counter increment would no longer roll back with a failed create
  (`src/ClaimsModule.Persistence/ClaimNumbers/ClaimNumberGenerator.cs:29` refuses to run outside a transaction).
- **Jobs could see rolled-back data:** Hangfire jobs could be enqueued for rows that then roll back.
- **Duplicate-key races would be 500s:** today they map to 409 (`UnitOfWork.cs:64-75`).

Proven by `Int: AUD_I4_A_failing_command_leaves_no_claim_no_audit_row_and_no_used_claim_number` (`tests/…/Claims/UnitOfWorkTests.cs:30`).

**4. How are reads separated from writes?**
- Commands and queries are different marker types: `ICommand<T>` / `IQuery<T>` (`src/ClaimsModule.Application/Common/Messaging/`). Naming
  is tested by `CONV_13_*`.
- **Commands** load the aggregate through `IClaimRepository` and call one domain method.
- **Queries** never load aggregates. They call SQL projections behind `IClaimQueries`
  (`src/ClaimsModule.Application/Abstractions/ReadModels/IClaimQueries.cs:14`), implemented in
  `src/ClaimsModule.Persistence/ReadModels/ClaimQueries.cs`. Application may not reference EF Core, and the list needs shadow columns and
  correlated subqueries.
- Query counts are fixed and tested (no N+1): the list is 2 SQL commands and the detail 7
  (`Int: API_02_List_runs_a_fixed_number_of_queries_whatever_the_page_size`).

**5. Where is FluentValidation wired, and what does a failure look like?**
- `src/ClaimsModule.Application/Common/Behaviors/ValidationBehavior.cs:26-41` runs every validator in the MediatR pipeline, sequentially
  (they share a scoped DbContext). It groups the errors per field and throws `ValidationException`.
- `src/ClaimsModule.API/Middleware/ExceptionHandlingMiddleware.cs:48` maps that to 422 with exactly `{type, title, status, errors}` (FRS
  §10.4).
- Model binding cannot pre-empt it: `SuppressImplicitRequiredAttribute…` is on, and lenient converters bind unknown enum values and
  malformed dates, so the FRS wording always wins (D-40 item 3).
- Test: `Int: API_ERR_422_body_matches_FRS_10_4_exactly`.

**6. How do domain events reach the audit log, and why is the Hangfire enqueue different?**
- Aggregate methods raise events. For example, `ApproveReserveTransaction` raises `ReserveApproved` at
  `src/ClaimsModule.Domain/Claims/Claim.Reserves.cs:140`.
- `UnitOfWork.DispatchBeforeCommitAsync` (`UnitOfWork.cs:81-103`) runs `ClaimAuditTrail` (`src/ClaimsModule.Application/Claims/Audit/ClaimAuditTrail.cs`).
  It writes through `IAuditLogService` (`src/ClaimsModule.Persistence/AuditLogService.cs:17`), in the same transaction.
- **After commit**, `GlPostingEnqueuer` (`src/ClaimsModule.Application/Claims/Jobs/GlPostingEnqueuer.cs:15`) enqueues the GL job.
- The two phases have two explicit interfaces, because the phase is the important fact about a handler:
  - audit must commit *with* the state;
  - a job must never see uncommitted rows, or run after a rollback.
- Tests:
  - `App: AUD_I1_Every_domain_event_has_a_before_commit_audit_handler`;
  - `Int: AUD_I1_Only_the_audit_log_service_creates_audit_rows`, an IL scan;
  - `Int: CONV_15_After_commit_handlers_run_after_the_commit_and_never_after_a_rollback`.

### EF Core and data

**7. Show me the value conversions, shadow properties and global query filters.**
- **Conversions** (`src/ClaimsModule.Persistence/ClaimsDbContext.cs:60-71`):
  - `decimal` → `DECIMAL(19,4)`;
  - `DateTimeOffset` → precision 7;
  - every domain enum → `NVARCHAR(50)` text.
- **Shadow properties** (`src/ClaimsModule.Persistence/Conventions/ModelBuilderConventions.cs:43-65`): `OrganisationId`, `CreatedAt`,
  `UpdatedAt`, `UserCreated`, `UserModified`, `IsDeleted`, `DeletedAt`, and the `RowVer` columns. `AuditColumnsInterceptor` fills them.
- **Filters** (`ClaimsDbContext.cs:78-96`): one combined soft-delete + tenant filter per entity, parameterised per query on
  `CurrentOrganisationId`.
- Tests: `Int: CONV_02/03/04/05/08_*`, `SEC_04_*`.

**8. How does tenant isolation work, including in background jobs?**
- **Queries:** the filter compares `OrganisationId` with the context's tenant. With no tenant it compares with `Guid.Empty`, so it fails
  closed.
- **Writes:** `AuditColumnsInterceptor` stamps the tenant on insert and refuses cross-tenant writes
  (`src/ClaimsModule.Persistence/Interceptors/AuditColumnsInterceptor.cs:78-83`).
- **Jobs:** jobs have no user. `TenantDirectory` is the only place that calls `IgnoreQueryFilters()` (and re-applies the soft-delete
  condition by hand). `JobScopes.RunAsync` (`src/ClaimsModule.Infrastructure/BackgroundJobs/JobScopes.cs:16`) then runs the command in a
  scope with that tenant and a fresh correlation id (D-31).
- Tests: `Int: SEC_04_Other_tenants_rows_are_invisible_and_cannot_be_written`, `SEC_04_Cross_tenant_claim_is_not_found`.

**9. How is the audit log kept append-only?**
There are three layers (D-14):
1. `IAuditLogService` is the only writer. An IL scan checks this.
2. `src/ClaimsModule.Persistence/Interceptors/ImmutableRowsInterceptor.cs:59-60` throws on a modified or deleted audit entry.
3. An `INSTEAD OF UPDATE, DELETE` trigger (`src/ClaimsModule.Persistence/Migrations/20260927185650_InitialCreate.cs:740`) stops raw SQL and
   `ExecuteUpdate`.

In Azure, the API's database user also has `DENY UPDATE, DELETE` through `claims_app` (`infra/sql/grant-api-identity.sql`).
Test: `Int: BR_A_01_Db_trigger_blocks_raw_update_and_delete`.

**10. Why a counter table for claim numbers, not a SEQUENCE?**
- FRS §5.3 says "no gaps". A SEQUENCE hands out values outside the transaction and loses its cache on restart.
- The generator runs `UPDATE ClaimNumberCounters SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue` *inside* the create transaction
  (`src/ClaimsModule.Persistence/ClaimNumbers/ClaimNumberGenerator.cs:57-64`).
  - The row lock serialises concurrent creates.
  - A rollback takes the increment back.
  - The first claim of a year inserts the row; a racing insert catches the PK violation and retries (`:36-52`).
- Test: `Int: BR_C_04_50_parallel_creates_yield_unique_gap_free_numbers`, mutation-checked. A read-then-write version produced a duplicate,
  and the unique index refused it.

**11. Why is ReserveComponent inside the Claim aggregate?**
- The $10M limit, the override, closure (CC-01/04), BR-C-06 and the read-only status all span components.
- As separate aggregates, two $6M approvals on *different* components would each read $0 approved, each pass, and the claim would end at
  $12M. That is write skew, and the component RowVers would never collide.
- Every change inside the aggregate marks the Claim row modified
  (`src/ClaimsModule.Persistence/Interceptors/AuditColumnsInterceptor.cs:57`), so the Claim RowVer serialises them → 409.
- Test: `Int: BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit`. Mutation check: removing the touch makes it fail.

### Domain rules

**12. Walk me through the authority thresholds. Is it ≤ or <?**
- `src/ClaimsModule.Domain/Reserves/ReserveAuthorityPolicy.cs:15-24`:
  - `|amount| ≤ 10,000` → Auto;
  - `≤ 100,000` → Supervisor;
  - otherwise → Manager.
- It uses the *single transaction's* absolute value (FRS §6.3). A large release needs the same authority as a large increase (D-05).
- `CanApprove` is hierarchical, so a manager can approve a supervisor-tier transaction.
- Boundaries are tested at 10,000 / 10,000.01 / 100,000 / 100,000.01, both signs (`Dom: BR_R_02_Tier_boundaries`,
  `tests/ClaimsModule.Domain.Tests/Reserves/ReserveAuthorityTests.cs:21`).
- The UI mirrors it in `web/claims-ui/src/app/shared/domain/authority.ts:11-21` (`Web: RSV_09_*`).

**13. How do you stop self-approval? When is it 403 and when 422?**
- The aggregate collects every violation (`src/ClaimsModule.Domain/Claims/Claim.Reserves.cs:121-137`):
  - submitter == approver → "Self-approval is not permitted.";
  - role below the tier → "Your role does not have authority to approve this reserve amount.";
  - the $10M limit re-checked.
- All three are 422 with the FRS §8 messages.
- A handler calling approve at all gets **403** from the endpoint policy (`ReservesController.cs:61`): that role can *never* approve.
  422 is used when it depends on the data (D-25).
- Tests: `Int: BR_R_03_Self_approval_returns_422_with_exact_message`, `Int: SEC_02_Handler_approve_returns_403`,
  `Int: BR_R_02_Supervisor_approving_above_100000_returns_422`.

**14. Explain the $10M aggregate limit and the override.**
- `ApprovedAggregate` = Σ `CurrentAmount` of the cost components. Subrogation is excluded, because it is an expected recovery (D-11).
- **At submission:** a transaction that would take approved reserves over $10M, with no override, is escalated to Manager and returns the
  FRS warning (`Claim.Reserves.cs:83-84, 106`).
- **At approval:** the limit is checked again, because other approvals may have moved the total (`:131-135`).
- **The override:** only a Manager can set it, with a reason (`:191-210`; the endpoint is Manager-only, `ClaimsController.cs:128-129`).
- The limit itself: `WouldExceedAggregateLimit` (`:313-317`). Exactly $10M is allowed; a decrease never trips it.
- Tests: `Dom: BR_R_05_*` (8), `Int: BR_R_05_Crossing_10M_warns_escalates_and_waits_for_the_override`.

**15. Walk me through the claim state machine.**
- The FRS §4.2 rows are declared once in `src/ClaimsModule.Domain/Claims/ClaimStatusTransition.cs:56-70`. They are seeded with `HasData`,
  and the aggregate is given the rows loaded from the table, so the rule is data.
- `Claim.ChangeStatus` (`src/ClaimsModule.Domain/Claims/Claim.Status.cs:18-99`) runs these checks in order:
  1. The row must exist, or 422 lists the valid next statuses.
  2. The minimum role, or 403.
  3. The reason, if the row requires one.
  4. The entry conditions:
     - Open: BR-ST-02, D-18, D-19 (`:102-125`);
     - PendingPayment: an approved reserve exists;
     - Closed: CC-01..04, every failure listed (`:128-152`);
     - Withdrawn: no pending reserve.
- Reopen writes STATUS_CHANGED, CLAIM_REOPENED, STATUS_CHANGED (`:158-173`).
- `GET /api/reference/claim-statuses` serves the same rows.

### Hangfire

**16. How is the GL posting job idempotent, even when two copies run at once?**
- It never checks and then acts.
- `src/ClaimsModule.Persistence/GlPosting/GlPostingStore.cs:27-38` runs one conditional UPDATE. Its predicate (`:71-77`) is: all three job
  arguments match, `PostingStatus = 'Pending'`, and the transaction is approved.
- `src/ClaimsModule.Application/Claims/Commands/PostGlReserveChange/PostGlReserveChangeCommand.cs:48-58` then acts only if exactly 1 row
  changed: it posts to the ledger with the key `Reserve:{componentId}:Change:{seq}` and stages GL_POSTING_SIMULATED, all in one
  transaction.
- A concurrent copy blocks on the row lock, re-evaluates the predicate against the committed row, and changes 0 rows.
- Backstops: `UX_ReserveHistory_IdempotencyKey`, and a unique filtered index allowing one GL_POSTING_SIMULATED per transaction.
- Test: `Int: BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry` (`tests/…/Jobs/GlPostingJobTests.cs:60`) waits until SQL Server shows
  the second run *blocked* before releasing the first.
- The same argument holds under RCSI (Azure SQL): an UPDATE always qualifies rows against the latest committed version.

**17. What happens when the ledger fails, or the process dies after commit?**
- **Retries:** `[AutomaticRetry(Attempts = 3)]`, 10/30/60 s (`src/ClaimsModule.Infrastructure/BackgroundJobs/PostGLReserveChangeJob.cs:26-32`).
  Each failed run rolls back, so the row stays Pending.
- **The last attempt** (`:61-80`) sets `Failed` with the same compare-and-set on `Pending`, writes GL_POSTING_FAILED in a new unit of work,
  and rethrows so the dashboard shows the job Failed.
- **Recovery:** `POST …/retry-posting` puts it back to Pending, audited and re-enqueued (`Claim.Reserves.cs:218-231`).
- **Lost enqueue** (crash between commit and enqueue): `GlPostingSweeperJob` re-enqueues approved rows still Pending after 5 minutes. The
  ReserveHistory row *is* the outbox (D-15).
- Tests: `Int: JOB_06_*`, `Int: JOB_11_Sweeper_enqueues_stranded_postings`, `Int: API_25_Retry_failed_posting`.

**18. The brief says the SLA job flags claims with a "SlaBreached status". Why doesn't yours?**
- FRS §12.2 says "do not change claim status", and D-01 follows the FRS.
- A flag column would also bump `UpdatedAt` through the interceptor and reset the 48-hour clock the job measures.
- The query (`src/ClaimsModule.Persistence/ReadModels/SlaMonitoringQueries.cs:28-34`) takes Draft/Open claims with
  `COALESCE(UpdatedAt, CreatedAt) < now − 48h` (strictly older, `SlaPolicy.cs:11`) and no breach entry in the last 24h.
- Each gets one SLA_BREACH_DETECTED row. The list derives `IsSlaBreached` from the audit log.
- Tested with `FakeTimeProvider`: `Int: JOB_08_Exactly_48_hours_is_not_yet_a_breach`, `JOB_09_*`, `JOB_10_Sla_job_does_not_modify_claim_row`.

### Concurrency, API and documents

**19. Two supervisors click Approve on the same reserve at the same moment. What happens?**
- Both load the claim and pass every check.
- The first commit updates the transaction, the component (RowVer) and the claim (RowVer).
- The second's UPDATE waits, matches 0 rows on RowVer, and gets `DbUpdateConcurrencyException` → 409. Its audit row rolls back, and nothing
  was enqueued.
- The test forces the interleaving with a before-commit gate instead of hoping for it (`Int: RSV_07_Concurrent_approvals_one_wins`,
  `tests/…/Reserves/ReserveConcurrencyTests.cs:43`).

**20. What does Idempotency-Key do, and why is it a filter and not a MediatR behaviour?**
- What gets replayed is an HTTP response (status, body, Location), which MediatR never sees.
- `src/ClaimsModule.API/Idempotency/IdempotencyFilter.cs:24` stores (user, key, method, route, SHA-256 of the body or form content) and
  replays a 2xx response.
- The same key with a different body gives 422; a request still in flight gives 409. Failures release the key.
- Tests: `Int: API_IDEMP_*` (8).

**21. How is document storage made testable, and how do you stop path traversal?**
- **Interface:** `IStorageService` (`src/ClaimsModule.Application/Abstractions/IStorageService.cs:9`) has an Azure implementation and a local
  one. The implementation is chosen from `Storage:Provider`
  (`src/ClaimsModule.Infrastructure/Storage/StorageRegistration.cs:33-36`).
- **Azure downloads** are a 1-hour read-only user-delegation SAS (`AzureBlobStorageService.cs:55-63, 109`), so the bytes never pass through
  the API.
- **File names:**
  - `SanitisedFileName.From` (`src/ClaimsModule.Domain/Documents/SanitisedFileName.cs:48`) normalises to NFKC, keeps the last path segment,
    and strips invisible and bidi characters.
  - `DocumentBlobPath.For` builds `{org}/{claim}/{docId}_{name}`.
  - The local provider re-checks containment.
- **Content:** it is sniffed against the declared type (`DocumentContentInspector`).
- Tests: `Dom: BR_D_01_*` (11), `Int: BR_D_02_*` against Azurite, `Int: DOC_05_*`.

### Frontend and Azure

**22. How is the FNOL reactive form designed?**
- `web/claims-ui/src/app/features/fnol-intake/fnol-form.ts:34` creates one typed `FormGroup` per step under a `linear` `MatStepper`, so each
  step validates independently.
- Validators return the API's own messages (`web/claims-ui/src/app/shared/forms/validators.ts`):
  - `lossDateValidator(now)` at `:29`, which reads an injected clock;
  - `lossDescriptionValidator` (20 trimmed characters) at `:42`;
  - `atLeastOneClaimant` on the parties FormArray at `:57`.
- The time picker is a separate control merged into the date, because of the Phase 6 bug (see AI-WORKFLOW §5 item 6).
- Server 422 keys are mapped back to control paths and to their step (`web/claims-ui/src/app/shared/forms/server-errors.ts:21-39`,
  `fnol-form.ts:131`).
- Tests: `Web: UI_FNOL_*`.

**23. How is the UI gated by role, and how do you guarantee no component calls HTTP directly?**
- `web/claims-ui/src/app/shared/domain/reserve-actions.ts:39-62` decides which buttons a row shows:
  - Approve/Reject only for supervisor+ on pending rows, disabled with the API message for self-approval or too little authority;
  - Retract only for the submitter;
  - Retry only for a Failed posting.
- The backend still decides everything.
- `HttpClient` is banned outside `core/api` by `no-restricted-imports` (`web/claims-ui/eslint.config.js:47`).
- `authInterceptor` adds the Bearer token only for API URLs, and `errorInterceptor` turns ProblemDetails into snackbars
  (`web/claims-ui/src/app/core/http/interceptors.ts:21, 50`).
- Tests: `Web: SEC_03_*`, `UI_GEN_03_*`, `UI_GEN_05_*`.

**24. Walk me through the Azure deployment. Where are the secrets?**
- **Runtime:** Container Apps (API + Hangfire), a Static Web App, serverless Azure SQL, Blob Storage and Key Vault. The API's only
  credential is a user-assigned managed identity:
  - SQL uses Entra-only authentication, and the API is a plain database user;
  - Blob uses Data Contributor on the container plus Delegator on the account, for user-delegation SAS (`infra/main.bicep:58, 209`);
  - the JWT key is a Key Vault reference (`infra/api.bicep:78`).
- **Pipeline:** GitHub signs in with OIDC, so no client secret is stored (`.github/workflows/deploy.yml:15`). It deploys the Bicep, runs the
  EF migrations bundle, grants the database user, rolls out a revision gated on `/health/ready`, uploads the SPA, and runs the smoke test.
- **Why Container Apps:** scale-to-zero lets the database pause (D-36). Before the review, set `minReplicas 1` (docs/DEPLOYMENT.md).

**25. How would you extend this to multi-currency reserves?** (brief §7.4)
- Add a currency to each reserve component and transaction, with an FX snapshot (rate, rate date, base-currency amount) on each
  transaction, so the history stays reproducible.
- Authority tiers and the $10M limit stay defined in the base currency, and are evaluated on the base amount.
- Components are keyed by (type, currency).
- The GL journal carries both amounts.
- An adjustment must be in the component's currency.

The files are listed in Part 2, task 5. Today, amounts are plain `decimal` with one scale guard, and USD is implicit (D-33, D-39 item 12).

---

## Part 2: rehearsal tasks for the live-implementation slot

Each task says where the change goes and what proves it. Keep the house rules: a message in `DomainMessages` or `RequestMessages`, the test
named after the rule ID, a matrix row, and a DECISIONS entry if the spec is silent.

### Task 1: a new validation rule

*"The police report number is required when the cause of loss is theft (`COL-THEFT`)."*

| Step | File |
|---|---|
| Message constant | `src/ClaimsModule.Domain/Claims/DomainMessages.cs` (new ASSUMPTION wording) |
| Rule: `RuleFor(c => c.PoliceReportNumber).NotEmpty().When(c => c.CauseOfLossCode == "COL-THEFT")` | `src/ClaimsModule.Application/Claims/Commands/CreateClaim/CreateClaimCommandValidator.cs` (next to the existing rules, lines 21–63) |
| Unit test `VAL_15_Police_report_required_for_theft` | `tests/ClaimsModule.Application.Tests/Claims/CreateClaimCommandValidatorTests.cs` |
| HTTP test (422 keyed `PoliceReportNumber`) | `tests/ClaimsModule.IntegrationTests/Claims/CreateClaimTests.cs` |
| UI: conditional validator on step 1, message shown inline | `web/claims-ui/src/app/features/fnol-intake/fnol-form.ts`, `shared/forms/validators.ts`, `steps/policy-loss-step.html` |
| Docs | matrix §4 (VAL-15), `docs/DECISIONS.md` (the rule is not in the FRS) |

The design question to be ready for: is this a pipeline rule (422 on POST) or a persisted completeness issue that blocks Open (D-06)? A
missing report can be supplied later, so D-06's logic points to an **issue**. That version touches
`Claim.Parties.cs` `EvaluateCompletenessIssues` (line 125) instead of the validator.

### Task 2: a new query endpoint

*"`GET /api/claims/{id}/parties` returns the claim's parties, with `?includeInactive=`."*

| Step | File |
|---|---|
| `ListClaimPartiesQuery : IQuery<IReadOnlyList<ClaimPartyDto>>` + handler (404 when the claim is not found) | new `src/ClaimsModule.Application/Claims/Queries/ListClaimParties/ListClaimPartiesQuery.cs` (copy the shape of `ListValidationIssues/ListValidationIssuesQuery.cs`) |
| Read contract | `src/ClaimsModule.Application/Abstractions/ReadModels/IClaimQueries.cs` (line 14) |
| SQL projection with `ProjectTo<ClaimPartyDto>` (the profile exists: `Claims/ClaimMappingProfile.cs`) | `src/ClaimsModule.Persistence/ReadModels/ClaimQueries.cs` (next to `ListValidationIssuesAsync`, line 161) |
| `[HttpGet]` on the parties controller | `src/ClaimsModule.API/Controllers/ClaimChildrenControllers.cs` (route at line 18) |
| Tests: 200 list, 404 unknown claim, cross-tenant 404, inactive filter | `tests/ClaimsModule.IntegrationTests/Claims/ClaimChildrenTests.cs` |

The CQRS naming test (`CONV_13_*`) and the no-EF-in-Application test (`CONV_14_*`) will check the shape for free.

### Task 3: a new filter on the claims list

*"Filter by severity" (or "only SLA-breached claims").*

| Step | File |
|---|---|
| Query parameter + validation (`IsInEnum`) | `src/ClaimsModule.Application/Claims/Queries/ListClaims/ListClaimsQuery.cs` (record line 16, validator 27, filter built at 51) |
| Filter record field | `src/ClaimsModule.Application/Abstractions/ReadModels/IClaimQueries.cs` (`ClaimListFilter`, line 66) |
| `Where` clause (SLA: the same `ClaimAuditLog.Any(...)` used for `IsSlaBreached`, line 60) | `src/ClaimsModule.Persistence/ReadModels/ClaimQueries.cs` (filters at lines 239–274) |
| `[FromQuery]` binding | `src/ClaimsModule.API/Controllers/ClaimsController.cs` (List, lines 46–58) |
| HTTP test | `tests/ClaimsModule.IntegrationTests/Claims/ClaimReadTests.cs` (`API_02_Each_filter_narrows_the_list`, line 49) |
| UI: model, URL ↔ query mapping, a control in the filter bar, the API call | `web/claims-ui/src/app/core/models/claim.models.ts` (`ClaimListQuery`, line 38), `features/claims-list/claims-query.ts` (lines 11, 31), `features/claims-list/claims-filter-bar.ts/.html`, `core/api/claims-api.service.ts` (line 46) |
| UI test | `features/claims-list/claims-list.spec.ts` (`UI_LIST_03_*`) |

Check that the fixed query count still holds (`API_02_List_runs_a_fixed_number_of_queries_whatever_the_page_size`).

### Task 4: modify a frontend component

*"Show the required authority on every pending row of the reserve history, and include the $10M escalation in the Add Reserve preview."*
This is also Phase 8 finding F3.

| Step | File |
|---|---|
| New column in the history table (the DTO already has `requiredAuthority`) | `web/claims-ui/src/app/features/claim-detail/tabs/reserves-tab.html` (column defs at lines 101–161), `reserves-tab.ts` (column list) |
| Escalation: `requiredAuthority(amount, { approvedAggregate, limit, overrideSet, component })` returns Manager when the approved total would cross $10M | `web/claims-ui/src/app/shared/domain/authority.ts`, `shared/ui/authority-indicator.ts` (new inputs), `features/claim-detail/tabs/add-reserve-panel.html` (line 69) |
| Tests | `web/claims-ui/src/app/shared/domain/authority.spec.ts` (`RSV_09_*`) |

The mirror must match `Claim.Reserves.cs` `WouldExceedAggregateLimit` (line 313): cost components only, increases only, `>` not `≥`.

### Task 5: multi-currency reserves (a design sketch, not a full build)

| Layer | Change |
|---|---|
| Domain | `Currency` (ISO 4217) on `ReserveComponent` and `ReserveTransaction`. An `FxSnapshot` (rate, as-of date, base amount) on each transaction. `ReserveAuthorityPolicy.RequiredAuthorityFor` and `Claim.WouldExceedAggregateLimit` use the **base** amount. `ApprovedAggregate` sums base amounts. `GlJournalEntry.ForReserveChange` carries the transaction and base amounts. Files: `src/ClaimsModule.Domain/Reserves/*.cs`, `Claims/Claim.Reserves.cs` |
| Application | An `IExchangeRateProvider` port. `SubmitReserveTransactionCommand` gets `Currency`, and the validator checks it is ISO 4217 and matches the component's currency. DTOs gain `currency` and `baseAmount`. Files: `Claims/Commands/SubmitReserveTransaction/`, `Claims/ReserveDtos.cs`, `Abstractions/` |
| Infrastructure | A rate-provider implementation (a fixed table for the demo) |
| Persistence | New columns `Currency NVARCHAR(3)`, `FxRate DECIMAL(19,8)`, `BaseAmount DECIMAL(19,4)`. The unique index `UX_ClaimReserveComponents_ClaimId_Component` becomes (ClaimId, Component, Currency) (`Configurations/ReserveComponentConfiguration.cs:31`). A migration backfills USD with rate 1 |
| UI | A currency select in the Add Reserve panel. `shared/ui/amount.ts` stops hard-coding USD (line 15). The authority preview uses the base amount the API returns |
| Tests | Tier boundaries at the converted edge, the limit summed across currencies, and mixed-currency adjustments rejected |

The argument to make: the FX rate is **snapshotted per transaction** (event sourcing), never recomputed. The authority and the limit are
defined in the base currency, because they express risk appetite, not a number in some currency.

---

## Appendix: phase-by-phase question bank (Phases 0–7)

### Architecture & CQRS
**Why is "submit reserve" a Command rather than a service call?**
It changes state. The command goes through the same pipeline as every other write: logging, then validation (422 before any transaction), then the UoW
(one transaction, audit before commit, enqueue after). A service call would have to repeat those concerns or skip them. The command is also a named,
testable unit that maps 1:1 to a business action and a matrix row. (ARCHITECTURE-PLAN §4–5)

**What would break if you removed the Unit of Work?**
Audit rows and state could commit separately. A crash between them gives either state with no audit row or an audit row for state that never happened.
Hangfire jobs could be enqueued for rows that then roll back. The claim-number counter would no longer share the claim's transaction, so a failed create
could leave a gap. (ARCHITECTURE-PLAN §4; D-10)

**Why is ReserveComponent inside the Claim aggregate when the FRS gives it its own RowVer?**
The $10M limit, the override flag, closure and the no-policy rule span components and the claim. As separate aggregates, two concurrent approvals on
different components could each pass the limit check (write skew). Touching the Claim row on every reserve command makes the Claim RowVer serialise them.
The component RowVer is still there. (ARCHITECTURE-PLAN §2.2)

**Why is idempotency an ASP.NET filter and not a MediatR behaviour?**
What gets replayed is an HTTP response (status, body, Location header). MediatR never sees those. (D-24)

### Data & EF Core
**Why a counter table instead of a SQL SEQUENCE for claim numbers?**
A SEQUENCE hands out a value even when the transaction rolls back, and its cache is lost on restart. Both create gaps, and FRS §5.3 says "No gaps".
`UPDATE … OUTPUT INSERTED` inside the create transaction rolls back with it. The row lock prevents duplicates. The cost is serialised creates per org
and year for the length of one short transaction. (D-10)

**How are primary keys generated if you need ids before SaveChanges?**
The domain assigns SQL-Server-friendly sequential GUIDs at construction. The column keeps its `NEWSEQUENTIALID()` default for non-EF inserts.
Guid v7 is avoided because SQL Server orders uniqueidentifiers by their last 6 bytes, so v7 would fragment the index. (D-30)

**How do background jobs cope with the tenant filter?**
There is no HTTP user in a job, so jobs set an explicit `ITenantContext`. In EF Core 9, `IgnoreQueryFilters()` removes soft-delete and tenant filters
together (named filters only arrive in EF 10), so any bypass re-applies `!IsDeleted` by hand, in one place. (D-31)

**Is ReserveHistory really immutable?**
Its amounts are immutable; the row is not. The FRS itself requires updating ApprovalStatus and PostingStatus on the row. The interceptor guards the
amount, balance, sequence and key columns. (D-22)

### Hangfire
**How is the GL job idempotent under concurrent execution?**
It never checks and then acts. One transaction runs a conditional `UPDATE … WHERE PostingStatus = 'Pending'` (and approved, and all three job
arguments match) and writes the audit row only if exactly 1 row changed. A concurrent duplicate blocks on the row lock, then updates 0 rows. The
backstop is a unique filtered index: at most one GL_POSTING_SIMULATED audit row per reserve transaction. (ARCHITECTURE-PLAN §6.1 R6, D-41)

**What if the process dies between commit and enqueue?**
The row still says `PostingStatus = Pending`. A 5-minute sweeper re-enqueues such rows, and the job's idempotency makes a double enqueue harmless. The
ReserveHistory row acts as the outbox. (D-15)

**Why doesn't the SLA job store a flag on the claim?**
Any write to Claims bumps `UpdatedAt`, and that would reset the 48h clock the job measures. The last breach is derived from the audit log, as FRS §12.2 says.
(D-01)

### Azure
**Why Container Apps and not App Service?**
Cost. Hangfire polls SQL all the time a server runs, so an always-on App Service keeps a serverless database awake and burns the free allowance. Container Apps
scales to zero when idle, the polling stops, and the database auto-pauses. The idle cost is about zero. The price is a cold start (container + database resume, absorbed
by EF connection retries), and SLA checks run only while a replica is up. The missed run fires on wake, and the rule is state-based, so only the detection time moves.
For the live review, `minReplicas` is set to 1. (D-36)

**What happens to a GL job if the container scales in mid-run?**
The job is persisted in SQL. After Hangfire's invisibility timeout, the next replica picks it up again. The conditional update makes a re-run a no-op if the first run
had already committed. (D-36, ARCHITECTURE-PLAN §6)

### Domain
**403 or 422 when a supervisor tries to approve $250k?**
422 with the FRS §8 message. The supervisor *can* approve, just not this amount, so the rule depends on the data. A handler hitting approve at all gets 403,
because that role can never perform the action. (D-25)

**How would you add multi-currency reserves?**
Today amounts are plain `decimal`s with one scale guard (D-39 item 12); a `Money` type was left out because value-converted types break SQL
translation of sums in the read models. For multi-currency, a Currency column goes on ReserveHistory and components. Authority thresholds and the $10M limit are defined in a base
currency, with an FX rate snapshot stored on each transaction (rate, rate date), so history stays reproducible. Components are keyed by (type, currency),
and the GL journal carries both amounts. Validation rejects mixed-currency adjustments on one component. (D-33)

**Why is "waive" not implemented?**
Under D-06, the only Critical issue stored on a claim is "no claimant". §4.2 and BR-ST-02 separately require a claimant, so waiving that issue could never
unblock anything. (D-07)

### Phase 1: skeleton & cross-cutting
**How do you prove the dependency rule holds, not just claim it?**
Two layers. Project references make illegal references impossible to compile (Domain has none; Infrastructure and Persistence know only Application). NetArchTest
then catches framework leakage through transitive references: Application must not touch EF Core, SqlClient, ASP.NET Core, Hangfire or Azure types, and
controllers must not touch Persistence. (`tests/ClaimsModule.IntegrationTests/Architecture/LayerDependencyTests.cs`)

**How do you enforce "no DateTime.Now"?**
An architecture test reads the IL of every production assembly with Mono.Cecil and fails on any call to `DateTime.Now/UtcNow/Today` or `DateTimeOffset.Now/UtcNow`.
It was mutation-checked: adding `_ = DateTimeOffset.UtcNow;` to `JwtTokenService` made it fail, naming the method. (`ClockUsageTests`, CONV-16)

**Why does ValidationBehavior run validators sequentially?**
Validators may run reference-data lookups on the request's scoped DbContext, and a DbContext does not support concurrent operations. There is also nothing to gain:
validation is dwarfed by the command's own I/O. (`ValidationBehavior.cs`)

**Why is `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` on?**
Otherwise `[ApiController]` rejects a missing non-nullable field with its own message before MediatR runs, so the FRS §8 wording could never appear, and
validation would effectively live in the controller layer. Now model binding only binds; FluentValidation in the pipeline decides. (D-38 item 2)

**Your 422 has no traceId. How do you correlate a failure?**
The `X-Correlation-Id` response header, which every log line of that request also carries. The body is kept to exactly FRS §10.4's four fields. (D-38 items 1, 10)

**Why accept a client-supplied correlation id at all, and why not reject a bad one?**
A client (the SPA, a gateway) can then tie its own logs to ours. The value lands in logs, audit rows (a GUID column, FRS §9.8) and a response
header, so only a GUID in the standard `D` format is accepted (D-39 Q1; the Phase 1 rule of "1–64 characters of `[A-Za-z0-9-_.]`" was
replaced). Anything else is replaced with a new GUID, not rejected: a tracing header should never fail a business request.
(`src/ClaimsModule.API/Middleware/CorrelationIdMiddleware.cs:34`)

**Why is AutoMapper on a version with a known high CVE?**
CVE-2026-32933 needs a ~25,000-level self-referencing graph to be mapped. We only map DB entities to DTOs, never request input, and no mapped type references itself.
Two tests enforce both conditions, and the suppression covers that single advisory (any other advisory still fails the build). The fixed versions are commercial.
If a test ever fails, the answer is to move to 16.x with a licence key. (D-37 amendment)

**Why are audit/soft-delete columns shadow properties?**
They are persistence bookkeeping, not domain state. The domain stays clean, one convention guarantees every table has them, and the interceptor (Phase 2) sets
them through the change tracker. Brief §6.1 names shadow properties explicitly. (D-38 item 4)

**Why are liveness and readiness different endpoints?**
Liveness failing makes Container Apps restart the container. If liveness checked SQL, a serverless database resuming from auto-pause (up to a minute) would cause
pointless restarts. Readiness includes the database, so traffic waits for it instead. (D-38 item 11, D-36)

**What stops the dev-token endpoint being a backdoor in production?**
`Auth:DevTokensEnabled` (false by default; the endpoints then answer 404). Tokens are still real HS256 JWTs validated for issuer, audience, lifetime, signature and
algorithm on every request, with the key from Key Vault. It is on in the demo deployment by decision (D-16), so reviewers can switch roles; a real system would put
an identity provider behind the same `JwtBearer` validation, with no change to the rest of the API.

### Phase 2: domain model & schema
**Where do the business rules live, and how do you know nothing bypasses them?**
In the Claim aggregate (`src/ClaimsModule.Domain/Claims/Claim*.cs`). Entities have no public setters (`DOM_01_Entities_have_no_public_setters`
checks every entity by reflection) and child collections are read-only wrappers. The only way to change a claim is a method that checks the rule.
Persistence adds backstops for the rules that matter most: the audit trigger, the amount guard, and unique indexes for claim numbers,
one pending transaction per component, and one GL idempotency key per row.

**The transition table is in the domain *and* in the database. Isn't that duplication?**
It is declared once, `ClaimStatusTransition.FrsDefaults()`. HasData seeds exactly those rows, and `CONV_10_Status_transitions_are_the_domain_table`
proves the database matches. At runtime the aggregate is given the rows loaded from the table (`StatusTransitionTable`), so a changed row changes
behaviour without a code change. That is how the brief's "Draft → Open → Closed … if configured" is met (D-20); the closure tests use it.

**Why does the aggregate load the whole reserve history?** (D-39 Q2)
Several rules need it: one pending per component, CC-01, "an approved reserve exists", CurrentAmount = Σ approved. With complete data the domain
cannot be fooled by a partial load. A claim has tens of transactions. If that grows, the stored `CurrentAmount` and `LastChangeSequence` projections
allow partial loading behind `IClaimRepository` without touching the domain.

**How do two concurrent approvals on different components not break the $10M limit?**
Any change inside the aggregate also touches the Claim row (`AuditColumnsInterceptor.TouchChangedClaims`: only `UpdatedAt` is marked modified).
EF then puts the Claim's RowVer into the UPDATE's WHERE clause, so the second commit on the same claim gets `DbUpdateConcurrencyException` → 409.
`CONV_08_Concurrent_changes_to_one_claim_conflict_on_RowVer` proves it with two different children. The domain also re-checks the limit at approval
(`BR_R_05_Limit_is_rechecked_at_approval`).

**Walk me through the claim-number generator.** (D-10)
`UPDATE ClaimNumberCounters SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue WHERE OrganisationId = @org AND Year = @year`, run inside the
claim's own transaction. The row lock serialises concurrent creates, and a rollback takes the increment with it, so there are no duplicates and no
gaps. The first claim of a year inserts the row; a racing insert gets a PK violation and retries the UPDATE. `BR_C_04_50_parallel_creates_…` runs 50
concurrent creates released at the same instant. I mutation-checked it: a read-then-write version produced a duplicate at the 6th claim, and the
unique index `UX_Claims_OrganisationId_ClaimNumber` refused it.

**What stops someone rewriting the audit log?**
Three layers (D-14). `IAuditLogService` is the only writer. `ImmutableRowsInterceptor` throws on a modified or deleted `ClaimAuditLog` entry.
The migration adds an `INSTEAD OF UPDATE, DELETE` trigger that throws error 51000. The trigger is what stops set-based `ExecuteUpdate` and raw SQL,
which never pass through the change tracker (`BR_A_01_Db_trigger_blocks_raw_update_and_delete`). A `claims_app` role with `DENY UPDATE, DELETE`
is ready for when the app stops connecting as dbo.

**ReserveHistory rows are updated on approval. Isn't that against event sourcing?**
The FRS itself puts ApprovedBy/At and PostingStatus on the row, so the row is *amount-immutable*, not row-immutable (D-22). The interceptor refuses
any change to amount, balances, sequence, key, reason or submitter, and any delete, even a soft one (`RSV_04_Modifying_a_history_amount_throws`).
A change of amount is always a new row.

**How does multi-tenancy work in the database?**
Every business table has `OrganisationId` (a shadow property except on Users). One global filter per entity combines soft delete and tenant,
`EF.Property<Guid>(e, "OrganisationId") == CurrentOrganisationId`, and EF evaluates that per query on the context instance. With no tenant the filter
compares with `Guid.Empty`, so it fails closed. The interceptor stamps OrganisationId on insert and refuses a cross-tenant insert or a changed tenant
(`SEC_04_*`). The cause-of-loss FK is composite `(OrganisationId, Code)`, so a claim cannot reference another organisation's code.

**Why `ValueGeneratedNever` on keys that have a NEWSEQUENTIALID() default?**
The domain assigns ids (D-30). With a store-generated key, EF assumes that a new child which already has an id (a party added to a loaded claim) is an
existing row, and issues an UPDATE instead of an INSERT. The immutability interceptor caught exactly this during Phase 2. The SQL default stays for
rows inserted outside EF.

**Why is `CorrelationId` a GUID and not the client's string?** (D-39 Q1)
FRS §9.8 types it as a GUID. The middleware therefore accepts a client id only in GUID form and otherwise generates one, which it echoes, so the client
can still correlate.

**CC-02 can never fail with the FRS table. Why keep it and how is it tested?**
The only persisted Critical is "no claimant". A claim reaches Closed only through Open, which needs a claimant, and the last claimant cannot be removed.
The check stays because the transition table is data. The tests add a configured Draft → Closed row, and CC-02/CC-03 then fail as specified
(D-39 item 17).

### Phase 3: commands, queries, controllers
**Walk me through POST /api/claims.**
Model binding creates `CreateClaimCommand`. Then, in order:
- `LoggingBehavior` logs the request.
- `ValidationBehavior` runs the FNOL validator (FRS §8 messages, reference-data lookups for BR-C-05 and the policy). Any failure is a 422
  before a transaction exists.
- `UnitOfWorkBehavior` opens the transaction inside the execution strategy.
- The handler draws the claim number (counter row, same transaction), builds the aggregate with `Claim.Create`, submits the optional
  initial reserve through the same domain method the reserve endpoint uses, and adds the claim to the repository.
- The Unit of Work collects the aggregate's events and runs `ClaimAuditTrail`, which stages the audit rows. It then saves, commits, and runs
  the after-commit handlers.
- The controller returns 201 with a Location header.

If anything throws, everything rolls back, the claim number included (`AUD_I4_*` proves this with a handler that throws after the audit rows
were staged).

**Why do queries go through Persistence "query" classes instead of IQueryable in the handler?** (D-40 item 1)
Application may not reference EF Core, and that is enforced by an architecture test. The list needs EF-only features: shadow columns
(`EF.Property(claim, "UpdatedAt")` for the SLA flag) and correlated subqueries. Application owns the contract and the DTO shape, and
Persistence owns the SQL. The query handlers stay thin, but they still carry validation and 404 mapping.

**How do you know there is no N+1?**
A test host adds a `DbCommandInterceptor` that counts SQL commands per request. The list is exactly 2 (COUNT and one page query with
subqueries) for page size 1 and for 100. The detail is 7 for a claim with 1 party and for one with 6 parties plus a reserve.

**Where does AutoMapper earn its keep here?**
`ProjectTo` projects the child rows (parties, risk objects, issues, documents, codes, users) in SQL, and the same profiles map in memory in the
command handlers. The claim rows are not mapped with AutoMapper: shadow columns and subqueries are clearer as a hand-written `Select`. The
self-reference guard for the suppressed CVE (D-37) runs over all the new maps.

**An unknown enum in the JSON body gives the FRS message, not a serializer error. How?** (D-40 item 3)
A lenient converter binds unknown enum names as 0, which no domain enum defines, so `IsInEnum()` reports "Invalid reserve component type.". An
empty or malformed optional date binds as null, which gives "Loss date is required.". Binding never words a 422; validators do.

**Why two handler interfaces instead of MediatR notifications for domain events?**
The phase is the important fact about a handler. A before-commit handler (audit) must share the transaction; an after-commit handler (a Hangfire
enqueue) must never see uncommitted rows or run after a rollback. The types make that visible, and a test proves the after-commit handler sees
the committed row and never runs after a rollback. The first version stopped at the first failing after-commit handler; a unit test caught it,
and failures are now isolated per handler.

**Idempotency: what exactly is stored, and why only 2xx?** (D-24, D-40 item 12)
The filter stores (user, key, method, route, SHA-256 of the body) plus the status, body and Location of a successful response. A repeat
replays those bytes with `Idempotency-Replayed: true`. A different body under the same key gets 422, and a request still in flight gets 409.
Failures release the key: after a 409 concurrency conflict or a 5xx the client should be able to retry, and a repeated 422 re-validates to
the same answer anyway. The placeholder row is committed before the command runs, so the unique (UserId, Key) index decides the race between
two concurrent duplicates.

**Why can an unknown policy id in the body be 422 but an unknown claim id be 404?**
404 means "the resource in the URL does not exist, or is not yours" (tenant filter). A bad reference inside a valid request is a validation
failure of that request.

**Why does a transition return 403 for a handler reopening, but 422 for a missing reason?**
The role can never make that move, whatever the data, so it is 403 (D-25). A missing reason is fixable input, so it is 422.

### Phase 4: reserves, GL posting, SLA job, Hangfire
**Walk me through the races you defend against.** (ARCHITECTURE-PLAN §6.1, written before the code)
Four mechanisms. (1) Every change inside a claim touches the claim row, so two commands on one claim conflict on its RowVer → 409. (2) Unique indexes
(ChangeSequence per component, idempotency key, one pending transaction per component) are the last word, and a duplicate key at commit is also a 409.
(3) Background writers only ever compare-and-set in one UPDATE. (4) Side effects happen after commit, and the sweeper covers a lost enqueue. Then the
table: same transaction approved twice (R1), same component adjusted twice (R2), write skew across components on the $10M limit (R3), crash between
commit and enqueue (R4), job before commit or after rollback (R5), GL job twice or concurrently (R6), partial failure inside a run (R7), the last failure
racing a success (R8), a manual retry racing a stray job (R9), the job racing a user on the claim (R10), overlapping SLA runs (R11).

**Two supervisors click Approve on the same reserve at the same moment. What happens, and how do you know?**
Both load the pending transaction and pass every check. The first commit updates the transaction, the component (RowVer) and the claim (RowVer). The
second's UPDATE waits for that lock, then matches 0 rows on the RowVer → `DbUpdateConcurrencyException` → 409. Its audit row rolls back with it, and it
enqueued nothing because enqueueing happens after commit. The test does not hope for a collision: a before-commit hook holds each request until both have
loaded the claim, so the worst interleaving is forced every run (`RSV_07_Concurrent_approvals_one_wins`).

**Approvals on different components touch different rows. How can two $6M approvals not both pass the $10M check?**
That is write skew, and the component RowVer cannot see it. Every command that changes anything in the aggregate also marks the claim row modified (the
audit-columns interceptor), so both approvals update `Claims` and the second conflicts. Its retry re-reads the claim and gets the 422 "Total reserves will
exceed $10,000,000". The mutation check: removing that one line of the interceptor makes `BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit` fail
(both approvals succeed, the claim ends at $12M), while the same-component race still passes on the component RowVer.

**Prove the GL job is safe when two copies run at once.**
`BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry`: run A claims the row, then its ledger call is paused inside the transaction. Run B starts, and
the test waits until `sys.dm_exec_requests` shows B blocked by A's lock. Only then is A released. A posts; B's UPDATE re-evaluates `PostingStatus = 'Pending'`
against the committed row and changes 0 rows. One ledger call, one audit row. Dropping the `Pending` predicate makes four tests fail.

**Does that still hold on Azure SQL, where READ_COMMITTED_SNAPSHOT is on?**
Yes. RCSI changes what plain SELECTs read, not how an UPDATE qualifies rows: the UPDATE takes an exclusive lock, and after waiting it evaluates the WHERE
clause against the latest committed row. Only SNAPSHOT isolation would work on a stale version, and it fails with an update-conflict error instead of
double-posting. The tests run on SQL Server's default (locking READ COMMITTED); the argument covers RCSI.

**Why `= 'Pending'` and not `<> 'Posted'` as CLAUDE.md says?** (D-41 Q1, accepted)
With `<> 'Posted'`, a Failed posting can be posted by any stray copy of the job, silently and without GL_POSTING_RETRIED, and that races the user's retry.
With `= 'Pending'`, Failed is terminal until the audited retry puts it back to Pending. Same single statement, same "1 row" rule; it only narrows what the
job may touch.

**What happens when the ledger keeps failing?**
Hangfire retries three times (10 s, 30 s, 60 s). Each failed run rolls its transaction back, so the row stays Pending and nothing is audited. The fourth run
knows it is the last (Hangfire's RetryCount job parameter), records `Failed` + GL_POSTING_FAILED with the reason in a new unit of work, and rethrows so
the dashboard shows the job as Failed too. A user then presses retry: Failed → Pending, audited, re-enqueued, 202. Demonstrated in the container with
`Jobs__GlPosting__SimulateFailure=true`.

**Why does the ledger call sit inside the database transaction? Isn't that a smell?**
For the simulation it is the simplest correct shape: claim the row, post, audit, commit, and any failure rolls everything back. With a real ledger I would
not hold a row lock across a network call. I would pass the idempotency key to the ledger (that is what `Reserve:{id}:Change:{seq}` is for, so a repeated call
cannot post twice), and move to "claim the row as Posting → call the ledger outside the transaction → mark Posted", with the sweeper recovering rows stuck
in Posting.

**Why doesn't the GL job update the claim like every other change does?**
It uses `ExecuteUpdate` on its own ReserveHistory row, which bypasses the change tracker, so the claim is not touched. A system posting should not reset the
SLA clock ("not updated in 48 hours" means by people), and it should not make a handler's concurrent edit fail with 409. `JOB_05` asserts the claim's
RowVer and UpdatedAt do not move.

**How does a background job know which tenant it is in?**
It has no user, so the tenant filter would hide everything. The GL job looks up its claim's organisation in `TenantDirectory`, the only place that uses
`IgnoreQueryFilters()` (and re-applies the soft-delete condition by hand, D-31); the recurring jobs loop over organisations. Each step then runs in its own DI
scope with the tenant and a fresh correlation id set, exactly like a request, and sends an Application command through the normal pipeline.

**How is the SLA job tested without waiting two days?**
The job tests run on a second API host with its own database and a `FakeTimeProvider`. The tests create claims, move the clock 48 hours and one second
(exactly 48 hours is not a breach: "older than"), run the job, move 24 hours (24 hours is enough: "24+ hours"), run it again. The claim row's Status,
UpdatedAt and RowVer are asserted unchanged.

**Why is the Hangfire dashboard read-only?**
Two reasons. Requeueing a failed GL job from the dashboard would bypass the audited retry (and would do nothing anyway, because the job only posts Pending
rows). And a dashboard authenticated by a cookie with POST buttons is a CSRF surface; with no POSTs there is none.

**How does a manager's browser get into the dashboard when the API uses Bearer tokens?**
Only under `/hangfire`: the first visit may carry `?access_token=` (the hand-off ASP.NET Core documents for SignalR). Once validated, the token is stored in an
HttpOnly, Secure, SameSite=Strict cookie scoped to `/hangfire` that expires with the token, and the Manager policy is enforced by the normal authorization
pipeline. Everywhere else, neither the query string nor the cookie counts (tested).

**What would you change for production?**
A real ledger client with the key (above); Hangfire's schema installed by the deployment rather than by the app at start-up (the app then needs no DDL rights);
per-tenant error isolation in the recurring jobs (today one tenant's failure stops the run, and the next run retries); batching the SLA insert for very large
books; and an alert on GL_POSTING_FAILED.


### Phase 5: documents

**Why does the upload command manage its own unit of work, when every other command gets it from the pipeline?**
Because it does network I/O that must not sit inside a database transaction and must not be replayed with it. The blob is written first, outside any
transaction; then one unit of work records the metadata and the DOCUMENT_UPLOADED audit row. Inside the pipeline's transaction, a 50 MB upload would hold a
pooled connection and an open transaction, and an execution-strategy retry (transient Azure SQL error) would run the handler again with a request stream that
can be read only once. The marker `IHandlesOwnUnitOfWork` makes this the one visible exception; it is still one transaction, with the same event dispatch (D-42 Q1).

**What if the metadata commit fails after the blob is written?**
The handler deletes the blob, unless the document row exists after all: a commit can succeed on the server while its acknowledgement is lost, and deleting then
would leave a row without a file. "When in doubt keep the blob": an orphan blob is invisible and harmless, a missing blob is a lost document. If the process dies
between upload and commit, the orphan stays; a Storage lifecycle rule or a sweep comparing blobs with rows would remove it (not built). Tests: `DOC_09_*`.

**How do you stop path traversal?**
Three layers. (1) `SanitisedFileName` keeps only the last path segment after NFKC normalisation, so `../`, `C:\`, UNC paths and fullwidth `．．／` all collapse
to a plain name; it also removes bidi overrides and zero-width characters and neutralises slash look-alikes. (2) `DocumentBlobPath` is always
`{orgGuid}/{claimGuid}/{docGuid}_{name}`: three segments, and the last one starts with a 36-character GUID, so even a name of `..` cannot become a
segment. (3) The local provider resolves every path and refuses anything outside its root; Azure has no directories to escape, and the aggregate refuses a path
of another claim. Mutation checks: switching NFKC back to NFC fails 15 tests; removing the local containment check fails 8.

**Why NFKC and not NFC?**
NFC only composes accents. NFKC also folds compatibility characters: fullwidth `．` and `／`, `‥` (two dot leader), ligatures. Those are exactly the "looks
harmless now, becomes `../` later" characters. The cost is that a few legitimate characters change (`ﬁ` becomes `fi`), which is acceptable for a file name (D-42 Q2).

**The Content-Type header is set by the client. How do you know a "PDF" is a PDF?**
Three checks: the extension must be on the allowlist; the declared type must not contradict it; and the bytes are sniffed. PDF, JPEG and PNG have
signatures at offset 0. DOCX and XLSX are ZIPs whose directory must hold `[Content_Types].xml` and `word/document.xml` or `xl/workbook.xml`, with no
`vbaProject.bin` (a renamed macro file). Text has no signature, so TXT/CSV must contain no binary bytes anywhere. The stored and served type is the
canonical one for the format, never the client's string.

**Can someone upload HTML as a .txt and get it executed?**
It passes sniffing, because it is text. But it is stored and served as `text/plain`, as an attachment (only PDF and images are inline), with the headers
fixed inside the signature, and the local endpoint adds `nosniff`. The browser downloads it; it never renders it.

**How is the SAS built, and why is there no account key in Azure?**
In Azure the API authenticates with its managed identity (DefaultAzureCredential) and asks Storage for a user delegation key, then signs a user-delegation SAS
with it: read-only, one blob, valid from 5 minutes ago (clock skew) to one hour from now, HTTPS only, with the content type and disposition fixed. The key is
cached for a day and renewed before it would expire under a new SAS, so the list costs no extra Storage call per document. With a connection string
(Azurite), the same SAS is signed with the account key.

**Can you revoke a SAS before its hour is up?**
Not individually: there is no stored access policy. The options are revoking all user delegation keys of the account (every delegation SAS dies at once), or
rotating the account key for account-key SAS. For one-hour, read-only, one-blob links that is the usual trade-off; a stored access policy would allow
revocation per container, but user-delegation SAS cannot use one.

**The FRS says bytes are never proxied through the API. The local fallback streams them. Is that a violation?**
It is the one documented deviation (D-28), limited to the development-only provider: a local disk has no SAS. The link still behaves like one: an HMAC-signed
token naming one file, its headers and its expiry, under a random per-process key. The endpoint exists only when the provider is LocalFileSystem, and
start-up refuses that provider outside Development.

**What happens to a 60 MB upload?**
The endpoint allows 51 MB of request body (50 MB plus the multipart envelope). A body declared larger gets 413 from a resource filter before anything reads it.
A file between 50 MB and 51 MB is read and gets the validator's 422 "The file must not exceed 50 MB.". Exactly 50 MB is accepted. The first container run showed
that without the filter, Kestrel's 413 was swallowed by form binding and came back as a 422 with an empty key.

**Does Idempotency-Key work for uploads?**
It did not until this phase: the filter hashed the raw body, and a multipart body has a random boundary, so a browser retry never matched. Forms are now hashed
by content (fields, file names, content types, SHA-256 of each file), so a retried upload replays the first response, and the same key with a different file is a 422.

**How would you scan uploads for malware?**
Microsoft Defender for Storage (malware scanning on upload) with its result written to blob index tags, and the list/URL endpoints refusing to sign a blob not
yet marked clean; or an Event Grid–triggered scanner. The upload flow stays the same; the document would get a `ScanStatus` column.

**How would you let the browser upload straight to Blob Storage?**
A two-step flow: `POST /documents/upload-url` returns a short-lived write-only SAS for the reserved path; the browser PUTs the file; `POST /documents/{id}/complete`
checks the blob (size, sniffed type) and records the metadata. It removes 50 MB from the API's memory and bandwidth; the price is a pending state and a
clean-up of uploads never completed.

### Phase 6: Angular frontend

**The FRS asks for "lazy-loaded feature modules". You have no NgModules. Is that a deviation?**
No, the intent is met: each feature (claims list, FNOL, claim detail) has one route file loaded with `loadChildren`, and `ng build` lists exactly three
named lazy chunks. Standalone route files are Angular's recommended form since v17; an NgModule would add a wrapper, not isolation (D-17, D-43 item 2).

**How do you guarantee components never call HTTP directly?**
Only `ClaimsApiService`, `ReferenceApiService` and `AuthService` import `HttpClient`; an ESLint `no-restricted-imports` rule fails `npm run lint`
anywhere else (tests excepted). A probe file importing `HttpClient` from a shared folder was rejected during the phase.

**The UI computes the authority tier and the closure checklist. Isn't that duplicating domain logic?**
Deliberately, and only for previews. The indicator must react while the user types, which a round trip cannot do. The copies are small pure functions in
`shared/domain`, named after the domain classes they mirror and tested at the same boundaries (10,000 / 10,000.01 / 100,000 / 100,000.01, negative
amounts by absolute value). The API decides again on submit; if the copy ever drifted, the user would see the API's 422, not a wrong result.

**Why is the Approve button hidden for a handler but only disabled for a supervisor on their own reserve?**
Two kinds of "no". A handler can *never* approve (the endpoint policy answers 403), so the button is hidden (SEC-03, brief §3.7.4). A supervisor can
approve in general, but not this row: self-approval or a tier above their authority. The button stays visible, disabled, with the API's own message
("Self-approval is not permitted.") as a tooltip, which explains the rule instead of hiding it. This mirrors the 403-vs-422 split of D-25.

**How does a 422 end up next to the right field?**
The API keys each error by the request's property path (`LossDate`, `Parties[0].FirstName`, `InitialReserve.Amount`, D-40 item 4). `applyServerErrors`
turns a key into a control path (with a small table for controls that live in a step group or have another name), sets a `server` error that the next
edit clears, and returns every key so FNOL also lists the messages at the top of the step they belong to, and selects that step.

**What happens if the user double-clicks Create, or the network drops after the server created the claim?**
The button is disabled while the request is pending, and the request carries one Idempotency-Key per FNOL form. A retry with the same key replays the first
201 (same claim number) instead of creating a second claim (D-24). The API releases the key after any non-2xx, so fixing a 422 and resubmitting works.

**Why is the list's filter state in the URL rather than in a service?**
A URL is shareable, survives a refresh and works with the back button, and it makes the list a pure function of the URL: every change of the query
params loads that page, and `switchMap` drops a slower response to an older filter. The two mapping functions are unit-tested.

**Why is `ClaimDetailStore` provided by the page component and not by the route?**
A route-level provider lives in the route's environment injector, which Angular keeps after you navigate away, so an old claim and a running GL poll
would outlive the page. Provided by the component, the store is created and destroyed with the screen.

**How does the UI show the GL posting finishing, when it happens in a Hangfire job?**
After an approval, while a row is approved but still `Pending`, the store re-reads the reserves every 3 seconds, at most 10 times, as a background request
(no progress bar). In the demo the badge turns from Pending to Posted within a few seconds. A push channel (SignalR) would be the production answer.

**How do you test "loss date cannot be in the future" without racing the clock?**
The validator reads the time from an injected `CLOCK` token, the front end's TimeProvider. Tests pin "now" and check one millisecond after it (rejected),
exactly now (accepted, no tolerance per D-32) and a later clock re-validating the same value.

**Why does the in-force badge use UTC dates?**
To say what the server will say. BR-C-02 compares the loss date's UTC calendar date with the policy period, inclusive (D-32). A loss at 23:30 in UTC−5 on
the expiry date is the next day in UTC, so the server records the warning; the badge must turn amber too. There is a test for exactly that case.

**Where did AI get the frontend wrong?**
See `docs/ai-log/phase-6.md` §5. The instructive one: binding a date picker and a time picker to the same FormControl, a pattern that looks right
but silently combines the time with *today*, because Angular writes a view change only to the model and not to sibling accessors. It was found by
driving the real form in a browser, not by the unit tests.


### Phase 7: Azure and CI/CD

**The brief lists App Service. Why is the API on Container Apps?**
Brief §2.3 allows either; §3.8's list is a minimum. Hangfire polls SQL for as long as a server runs, so an always-on App Service keeps a serverless database awake
and spends the free allowance in days. Container Apps scales to zero, the polling stops, and the database pauses 15 minutes later (D-36). For the review,
`minReplicas = 1`. (D-44, `docs/DEPLOYMENT.md`)

**Where are the secrets?**
There is one: the JWT signing key, in Key Vault. The pipeline generates it once with `openssl` and never prints it; the Container App reads it through a Key
Vault reference with its managed identity. SQL has no password (Entra-only authentication, the API connects as its managed identity), Storage has shared keys
disabled (user-delegation SAS), GitHub stores no secret (OIDC federated credential; the SWA deployment token is read at run time and masked). (D-44 Q1, items 4, 13)

**Why a user-assigned identity rather than system-assigned?**
Ordering. A system-assigned identity is born with the Container App, so its Key Vault role cannot exist before the first revision tries to resolve the secret
reference. A user-assigned identity and its role assignments are created first, in `main.bicep`, and the app just references it. (D-44 item 2)

**Why both Storage Blob Data Contributor and Storage Blob Delegator?**
Different scopes. Data Contributor is assigned on the `claim-documents` container only, so the API cannot touch any other container. Signing a SAS needs a
*user delegation key*, which is an account-level operation that a container-scoped role cannot grant; Delegator grants exactly that action and no data access.
(D-44 item 3)

**What can the API do to the database?**
Read and write rows. It is not dbo: it has `db_datareader`, `db_datawriter`, the `claims_app` role (DENY UPDATE, DELETE on ClaimAuditLog), `CREATE TABLE` and
ownership of the `[HangFire]` schema, so Hangfire can install its own tables there and nowhere else. It cannot alter `dbo`, drop or disable the audit trigger,
or update an audit row even with raw SQL. Checked locally with an equivalent user: Msg 229 on the UPDATE, 1088/3701 on the trigger, 2760 on a dbo table, and the
full smoke test still passes. (D-44 Q2, `infra/sql/grant-api-identity.sql`)

**How does the pipeline create a database user for a managed identity?**
The deployment service principal is the SQL server's Entra admin. After the migrations it runs the grant script with go-sqlcmd. The user is created
`WITH SID = <client id>, TYPE = E`: `FROM EXTERNAL PROVIDER` would make the server look the name up in Microsoft Graph, which for a service-principal admin
needs Directory Readers on the server. The script is idempotent. (D-44 item 7)

**How do migrations reach a database that is not open to the internet?**
The `infra` job adds a firewall rule for its own runner IP, runs the self-contained EF Core migrations bundle signed in as the service principal
(`Authentication=Active Directory Default` uses the `az` session from `azure/login`), and deletes the rule in an `always()` step. The API reaches SQL through
the "Allow Azure services" rule and still needs a token for a database user. Production: VNet integration + private endpoint, and the bundle run as a
Container Apps job inside the VNet. (D-44 items 5–6)

**Migrations run before the new code. What if a migration breaks the running version?**
Order is migrate → new revision, so each migration must work with the code already running (expand/contract: add a column, deploy, backfill, then drop in a
later release). The demo has no live traffic during a deploy, so this is a stated rule, not engineered tooling. (D-44 item 6)

**What if the deployment fails halfway?**
Every step is re-runnable: Bicep is declarative, the signing key is created only if missing, migrations are idempotent (`__EFMigrationsHistory`), the grant
script is idempotent, and `activeRevisionsMode: Single` keeps the old revision serving until the new one is ready. Re-run the failed jobs.

**How does the SPA know the API URL?**
It is a build-time constant: `environment.ts` reads `API_BASE_URL`, `angular.json` defaults it to localhost, and the workflow builds with
`ng build --define "API_BASE_URL='https://…'"`. The URL is known before the API is deployed (`<app name>.<environment default domain>`), so the SPA builds in
parallel with the API. A runtime `config.json` would allow build-once-deploy-anywhere; with one environment it is not worth the extra request. (D-44 item 11)

**How is CORS locked down, and how do you know?**
The API admits exactly one origin, `Cors__AllowedOrigins__0 = https://<swa>.azurestaticapps.net`. The smoke job sends a preflight from that origin
(`Access-Control-Allow-Origin` echoed) and from `https://evil.example` (no header). Bearer tokens, not cookies, so no credentials mode. (D-44 item 9, 15)

**What does the smoke test prove?**
The brief §7.2 demo flow over HTTP against the deployed API: FNOL → Open → a $25,000 reserve as a handler (PendingApproval, Supervisor) → the handler's
own approve refused (403) → a supervisor approves → the Hangfire GL job posts it (polled) → a PDF uploads, and its SAS URL returns the same bytes with
no Authorization header (managed identity + Delegator) → the audit log holds CLAIM_CREATED, STATUS_CHANGED, RESERVE_CREATED, RESERVE_APPROVED,
GL_POSTING_SIMULATED and DOCUMENT_UPLOADED. It waits out a cold start first. (`scripts/smoke-test.sh`)

**Why is there no approval gate before production?**
GitHub environments are not available for private repositories on the Free plan. The federated credential trusts only `refs/heads/main`, so a workflow
run from any other branch cannot sign in. With a public repository or a paid plan: `environment: production` with a required reviewer. (D-44 Q3)

**Why ghcr.io and not ACR?**
Free, and the image holds no secrets (all configuration comes from the environment and Key Vault), so it can be public and Container Apps pulls without a
registry credential. The price is one manual step: a new package is private and GitHub has no API to change that. The workflow checks an anonymous pull and
fails with the fix. ACR Basic (~USD 5/month, pull with the managed identity) is the fallback for a private image. (D-36, D-44 item 10)

**Actions are pinned to tags, not SHAs. Is that safe?**
Major tags are readable and receive fixes; a SHA pin protects against a compromised tag being moved. For a production pipeline, pin SHAs and let Dependabot
update them. Stated as a trade-off in D-44 item 14.

**What did you not verify?**
No Azure deployment was made in this phase. Verified locally: Bicep lint (0 warnings), actionlint, shellcheck, the bundle and go-sqlcmd with the workflow's
exact flags, and the smoke test against the local stack as dbo and as the restricted user. Only a real run proves token acquisition on the runner, `TYPE = E`
user creation, the Key Vault reference, user-delegation SAS and RBAC propagation timing. (D-44, "Not verified")
