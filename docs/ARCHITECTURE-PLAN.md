# Architecture plan (draft → becomes ARCHITECTURE.md in Phase 8)

Status: **DRAFT, Phase 0; §2.2, §2.3 and §3 revised in Phase 2 (D-39); §2.5, §3, §4.1 and §5 revised in Phase 3 (D-40); §6 revised and §6.1 (race conditions) added in Phase 4 (D-41)**. Based on the entries in `docs/DECISIONS.md`, all ACCEPTED on 2026-09-27 (D-36 changed to Container Apps).
Spec references: FRS = `docs/spec/claims-frs.md`, Brief = `docs/spec/assessment-brief.md`.

---

## 1. Solution structure (Brief §2.4, CLAUDE.md)

```
src/ClaimsModule.Domain          no dependencies
src/ClaimsModule.Application     → Domain
src/ClaimsModule.Infrastructure  → Application
src/ClaimsModule.Persistence     → Application
src/ClaimsModule.API             → Application, Infrastructure, Persistence (composition root only)
tests/ClaimsModule.Domain.Tests | Application.Tests | IntegrationTests
web/claims-ui
```
The dependency rule is enforced by a NetArchTest test (CONV-14). Application references neither EF Core nor Azure SDK types.

---

## 2. Aggregates and boundaries

### 2.1 Claim is the single aggregate root for the claim's write model

| Inside the Claim aggregate | Why it is inside |
|---|---|
| `LossEvent` (1:1) | Created atomically with the claim (FRS §10.1); BR-C-01/02/05/07 |
| `ClaimParty` (0..n) | BR-C-03 / CC-03 / last-Claimant rule need the full set |
| `ClaimRiskObject` (0..n) | the no-risk-object warning (FRS §5.4) |
| `ClaimValidationIssue` (0..n) | BR-ST-02, CC-02, D-07 |
| `ReserveComponent` (0..4, one per type) + its `ReserveTransaction` rows (the ReserveHistory table) | see §2.2 |
| `ClaimDocument` metadata (0..n) | the terminal-status read-only rule (D-26); uploaded via the aggregate so DocumentUploaded is raised consistently |

Outside the aggregate, referenced **by id only**: `Policy` (the simulated policy module), `CauseOfLossCode` (reference data), `User`, `Organisation`.
`ClaimAuditLog` is not part of any aggregate. It is a separate append-only write path through `IAuditLogService` (FRS §14.2).

### 2.2 Is ReserveComponent its own aggregate? No: it lives inside Claim.
FRS §15.1 calls both Claims and ClaimReserveComponents "aggregate roots" (they get RowVer). I argue that the component is an **entity inside the Claim
aggregate** that keeps its own RowVer column. This is a flagged DEVIATION in terminology, not in schema.

These invariants span components and the claim, so they must be checked against one consistent snapshot:
- **BR-R-05**: the approved total across *all* components is ≤ $10M, unless the *claim's* override flag is set.
- **BR-C-06**: no reserves without a policy (a claim field).
- **D-26**: no reserve changes on Closed or Withdrawn claims (a claim field).
- **§4.2**: → PendingPayment needs an approved reserve. **CC-01/CC-04**: closure inspects every component.

**The concurrency argument (write skew).** If components were separate aggregates, two supervisors could approve $6M transactions on *Indemnity* and
*Expense* at the same time. Each reads a total of $4M, each passes the check, each commits, and the claim ends at $16M. Two different component rows
were updated, so neither RowVer conflicts. Likewise, a "close claim" command could commit while an approval on a component commits concurrently.

**Design.**
- Every command that mutates anything in the aggregate also marks the `Claim` row as modified. `UpdatedAt` and `UserModified` change, so EF includes
  the Claim's RowVer in the UPDATE's WHERE clause.
- Any two concurrent commands on the same claim therefore conflict on the Claim RowVer. The loser gets `DbUpdateConcurrencyException` → 409 (CLAUDE.md
  rule 12), and the client re-reads and retries.
- The component's own RowVer additionally protects `LastChangeSequence` / `CurrentAmount`, which is belt and braces and satisfies FRS §15.1 literally.
- Cost: commands on the *same* claim are serialised optimistically. For a claims workload (a handful of users per claim) this is irrelevant.
- Side effect, intended: reserve activity counts as a claim update for the SLA clock (FRS §12.2 "not been updated").

Loading (revised in D-39 Q2): a command loads the **whole** aggregate, including every reserve transaction (split query). The rules (one pending
transaction per component, CC-01, "an approved reserve exists", CurrentAmount = Σ approved) are then checked against complete data. A claim has tens of
transactions; if that changes, the stored `CurrentAmount` projection and `LastChangeSequence` allow partial loading behind the repository.

### 2.3 Value objects
| VO | Purpose / invariant |
|---|---|
| `ClaimNumber` | `CLM-{YYYY}-{0000000}` format/parse (BR-C-04) |
| ~~`Money`~~ | Not built (D-39 item 12): amounts are `decimal` guarded by `Amounts.HasValidScale` (≤ 4 dp, fits DECIMAL(19,4)); currency implicit USD (D-33) |
| `PartyName` | Person → FirstName + LastName required; Company → CompanyName required (FRS §9.3) |
| `ContactInfo` | Email (format), Phone |
| `GlIdempotencyKey` | `Reserve:{ComponentId}:Change:{Seq}` (D-23) |
| `SanitisedFileName` | path-traversal-safe name (BR-D-01) |
| `ReserveAuthorityPolicy` (domain service) | \|amount\| → Auto / Supervisor / Manager; can a given role approve? (BR-R-02, D-05, D-11 escalation) |

### 2.4 Enums (stored as NVARCHAR(50) via value conversion)
ClaimStatus, ClaimSeverity, PartyRole, PartyType, AssetType, ReserveComponentType, ReserveComponentStatus, ReserveTransactionType,
ReserveApprovalStatus (AutoApproved/PendingApproval/Approved/Rejected/Cancelled), PostingStatus (Pending/Posted/Failed/Cancelled), DocumentType,
PolicyStatus, IssueSeverity, IssueStatus, UserRole. Audit event types are string constants (the list is open-ended, per BR-A-02).

### 2.5 Domain events and handlers
Events are raised by aggregate methods and collected by the UoW. There are two dispatch phases (CLAUDE.md rule 5):

| Event | Before commit (same transaction) | After commit |
|---|---|---|
| ClaimCreated | audit CLAIM_CREATED | — |
| ClaimStatusChanged | audit STATUS_CHANGED (+ CLAIM_CLOSED / CLAIM_REOPENED) | — |
| PartyAdded / PartyRemoved | audit PARTY_ADDED / PARTY_REMOVED | — |
| RiskObjectAdded | audit RISK_OBJECT_ADDED | — |
| ValidationIssueRaised / Resolved / Acknowledged | audit VALIDATION_ISSUE_ADDED / _RESOLVED / _ACKNOWLEDGED | — |
| PolicyLinked, HandlerAssigned, ClaimDetailsUpdated, ReserveLimitOverrideSet | audit | — |
| ReserveTransactionSubmitted | audit RESERVE_CREATED | — |
| ReserveAutoApproved | audit RESERVE_AUTO_APPROVED | **enqueue PostGLReserveChangeJob** |
| ReserveApproved | audit RESERVE_APPROVED | **enqueue PostGLReserveChangeJob** |
| ReserveRejected / ReserveRetracted | audit RESERVE_REJECTED / RESERVE_RETRACTED | — |
| GlPostingRetryRequested | audit GL_POSTING_RETRIED | **enqueue PostGLReserveChangeJob** |
| DocumentUploaded | audit DOCUMENT_UPLOADED | — |

**Mechanics (Phase 3, D-40).** Handlers implement `IBeforeCommitHandler<TEvent>` or `IAfterCommitHandler<TEvent>`: two explicit
interfaces rather than MediatR notifications, because the phase is the important fact about a handler. `UnitOfWork` collects the events
of every tracked aggregate after the command handler ran, dispatches the before-commit handlers (repeating until no new events appear),
saves, commits, and only then dispatches the after-commit handlers, outside the replayed execution-strategy block. An after-commit failure
is logged per handler and never fails the request. `ClaimAuditTrail` is the before-commit handler for all 20 events; a unit test fails if
an event has none (AUD-I1). Phase 3 has no production after-commit handler yet: the GL enqueue arrives in Phase 4.

**Why the split.** Audit rows must commit or roll back *with* the state change (no divergence), so they are written before commit. A Hangfire job must
never see uncommitted rows, and must not run for a transaction that rolls back, so it is enqueued after commit. The window between commit and enqueue is
closed by the GL sweeper (D-15). The jobs themselves write audit rows directly through `IAuditLogService`, because they do not go through an aggregate.

---

## 3. Data model

Legend: **SD** soft delete (IsDeleted, DeletedAt + filter) · **T** OrganisationId + tenant filter · **RV** RowVer · **AC** audit columns
(CreatedAt, UpdatedAt, UserCreated, UserModified). All PKs are `UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()`, with values assigned in the domain (D-30).
SD and AC columns are EF Core **shadow properties** added to every entity by one convention (`ModelBuilderConventions.ApplyStandardColumns`, D-38);
Organisations and Users exist since Phase 1.

| Table | Key columns | FKs | Indexes | SD | T | RV | AC |
|---|---|---|---|---|---|---|---|
| Organisations | Id, Name | — | — | ✓ | — (is the tenant) | — | ✓ |
| Users | Id, OrganisationId, Username, DisplayName, Role, IsActive | Organisations | UX Username (system-wide: sign-in precedes the tenant, D-38) | ✓ | ✓ | — | ✓ |
| Policies | Id, PolicyNumber, ClientName, EffectiveDate DATE, ExpirationDate DATE, Status, CoverageTypes (JSON) | — | UX (OrganisationId, PolicyNumber); IX ClientName | ✓ | ✓ | — | ✓ |
| CauseOfLossCodes | Id, Code, Name, PerilCategory, IsActive, SortOrder | — | UX (OrganisationId, Code) | ✓ | ✓ | — | ✓ |
| ClaimStatusTransitions | Id, FromStatus, ToStatus, MinimumRole, RequiresReason, IsSystemOnly | — | UX (FromStatus, ToStatus) | — | — (global) | — | ✓ |
| ClaimNumberCounters | (OrganisationId, Year) PK, LastValue INT | — | PK | — | n/a | — | — |
| **Claims** | Id, ClaimNumber, PolicyId?, PolicyNumber?, ClientName?, Status, Severity, ReportedDate, AssignedHandlerId?, ClosedAt?, ClosureReason?, Notes?, ReserveLimitOverride BIT, ReserveLimitOverrideByUserId?, ReserveLimitOverrideAt? | Policies, Users | UX (OrganisationId, ClaimNumber); IX (OrganisationId, Status, UpdatedAt) for list+SLA; IX AssignedHandlerId; IX PolicyId | ✓ | ✓ | ✓ | ✓ |
| LossEvents | Id, ClaimId, LossDate, LossDescription, LossLocation?, CauseOfLossCode, EstimatedLossAmount? DECIMAL(19,4), ReportDate, PoliceReportNumber? | Claims; CauseOfLossCodes(OrganisationId, Code) alternate key | UX ClaimId; IX LossDate; IX CauseOfLossCode | ✓ | ✓ | — | ✓ |
| ClaimParties | Id, ClaimId, PartyRole, PartyType, FirstName?, LastName?, CompanyName?, Email?, Phone?, Notes?, IsActive | Claims | IX (ClaimId, PartyRole, IsActive) | ✓ | ✓ | — | ✓ |
| ClaimRiskObjects | Id, ClaimId, AssetType, AssetDescription, DamageDescription?, IsPrimary, AssetReference? | Claims | IX ClaimId | ✓ | ✓ | — | ✓ |
| ClaimValidationIssues | Id, ClaimId, RuleCode, Severity, Field, Message, Status, RaisedAt, ResolvedAt?, ResolvedByUserId?, ResolutionNote? | Claims | UX (ClaimId, RuleCode) WHERE Status IN ('Open','Acknowledged') AND IsDeleted=0 (D-39 item 7) | ✓ | ✓ | — | ✓ |
| **ClaimReserveComponents** | Id, ClaimId, Component, CurrentAmount DECIMAL(19,4), LastChangeSequence INT, Status, Notes? | Claims | UX (ClaimId, Component) WHERE IsDeleted=0 | ✓ | ✓ | ✓ | ✓ |
| ReserveHistory | Id, ReserveComponentId, ClaimId, TransactionType, Amount, PreviousBalance, NewBalance, ApprovalStatus, RequiredAuthority, ApprovedByUserId?, ApprovedAt?, RejectedByUserId?, RejectedAt?, RejectionReason?, ChangeReason, PostingStatus, PostingJobId?, IdempotencyKey, ChangeSequence, SubmittedByUserId, ExceedsAggregateLimit BIT | Components, Claims | UX (ReserveComponentId, ChangeSequence); UX IdempotencyKey (every row has a key, D-39 item 6); UX ReserveComponentId WHERE ApprovalStatus='PendingApproval' (D-22); IX (ClaimId, CreatedAt); IX (ApprovalStatus, PostingStatus, ApprovedAt) for the sweeper | ✓ | ✓ | — | ✓ (amount columns guarded, D-22) |
| ClaimDocuments | Id, ClaimId, DocumentType, DocumentName, BlobPath, ContentType, FileSizeBytes BIGINT, UploadedAt, UploadedByUserId?, Notes? | Claims | IX ClaimId | ✓ | ✓ | — | ✓ |
| **ClaimAuditLog** | Id, ClaimId, EventType, Description, OldValue?, NewValue?, RelatedEntityId?, RelatedEntityType?, CorrelationId?, CreatedAt, CreatedByUserId? | Claims | IX (ClaimId, CreatedAt DESC); IX (ClaimId, EventType, CreatedAt) for SLA dedupe | **—** (D-14) | ✓ | — | CreatedAt/CreatedByUserId only |
| IdempotencyRecords (Phase 3, D-24, D-40 item 12) | Id, OrganisationId, UserId, Key, Method, Route, RequestHash CHAR(64), StatusCode?, ResponseBody?, ResponseLocation?, CreatedAt, CompletedAt? | Organisations | UX (UserId, Key); IX CreatedAt (clean-up) | — | column only (no filter: keys are per user) | — | CreatedAt only |
| Hangfire.* | Hangfire's own schema (`[HangFire]`) in the same database | — | — | — | — | — | — |

Seed data (HasData or migration SQL only, FRS §15.4):
- 1 Organisation.
- 6 Users (D-16).
- The 5 FRS §5.5 policies exactly, plus 3 long-dated extras (D-34).
- The 10 FRS §5.6 codes exactly.
- 12 transition rows (D-09).
- The audit-log `INSTEAD OF UPDATE, DELETE` trigger (D-14).

---

## 4. CQRS flow: `POST /api/claims/{id}/reserves` end to end

```
HTTP POST (Bearer JWT, X-Correlation-Id, optional Idempotency-Key)
 → CorrelationIdMiddleware        read/create id; push into the log scope; ICorrelationContext
 → SerilogRequestLogging          one structured line per request, with the final status code
 → ExceptionHandlingMiddleware    maps exceptions → ProblemDetails (422/404/403/409/500)
 → Authentication/Authorization   JWT validated; [Authorize] (any role may submit)
 → IdempotencyFilter (D-24)       replay the stored response if the key was seen
 → ReservesController.Submit      binds the body → SubmitReserveTransactionCommand; ISender.Send
   ↓ MediatR pipeline
   LoggingBehavior                structured start/end/duration; command name; correlation id
   ValidationBehavior             FluentValidation (shape: component enum, amount ≠ 0, reason length) → ValidationException → 422
   UnitOfWorkBehavior             (ICommand<T> only) BEGIN TRAN inside the EF execution strategy
     SubmitReserveTransactionHandler
       claim = IClaimRepository.GetForReserveChange(id)    tenant + soft-delete filters apply
       claim.SubmitReserveTransaction(component, amount, reason, type, currentUser, authorityPolicy, now)
         domain checks: status not terminal (D-26), policy linked (BR-C-06), BR-R-01 sign rules (D-05),
         no pending on the component (D-22), ChangeSequence = last+1 (D-23), tier = f(|amount|) (BR-R-02),
         aggregate limit → warning/escalation (BR-R-05, D-11); ≤10k & no escalation → AutoApproved + CurrentAmount recomputed
         raises ReserveTransactionSubmitted (+ ReserveAutoApproved); touches the Claim row (RowVer, §2.2)
       returns the DTO (AutoMapper, Application profile)
     IUnitOfWork.CommitAsync()
       1. dispatch before-commit events → audit handlers → IAuditLogService.Add(...)  (rows added to the same DbContext)
       2. SaveChangesAsync  (interceptors: audit columns, tenant stamping, append-only guard, amount-column guard)
       3. COMMIT
       4. dispatch after-commit events → IBackgroundJobScheduler.EnqueueGlPosting(historyId, claimId, key)
 ← 201 Created { transaction, approvalStatus, requiredAuthority, warnings[] }
```
What breaks without the UoW: audit rows and state could commit separately. A crash between them leaves state with no audit row, or an audit row for
state that never happened. Jobs could also be enqueued for data that then rolls back.

**Azure SQL retries.** With `EnableRetryOnFailure`, a user-initiated transaction must run inside `strategy.ExecuteAsync(...)`. The UnitOfWorkBehavior wraps
the handler call and the commit in the strategy, so a transient failure replays the whole unit. Handlers are deterministic given the same input and
TimeProvider, so a replay is safe. The after-commit dispatch runs outside the replayed block.

### 4.1 The read side (Phase 3, D-40)
Queries do not load aggregates. Application defines read contracts (`IClaimQueries`, `IReferenceDataQueries`, `IPolicyQueries`,
`IUserQueries`) and the DTO shapes; Persistence implements them as SQL projections. The claim rows are hand-written `Select`s,
because they read shadow columns (UpdatedAt, CreatedAt) and correlated subqueries (cause and handler names, net reserve total, SLA flag).
The simple child rows use AutoMapper `ProjectTo` with the Application profiles. Query counts are fixed and tested: the list runs 2 SQL
commands (COUNT + page) and the detail runs 7, whatever the page size or the number of children. The tenant and soft-delete filters
apply to every table they read.

---

## 5. MediatR pipeline behaviours (order)
1. **LoggingBehavior** (outermost): times and logs everything, including validation failures.
2. **ValidationBehavior**: runs all `IValidator<TRequest>`, aggregates the errors, and throws `ValidationException` → 422 with the FRS §10.4 shape. It runs
   before any transaction is opened.
3. **UnitOfWorkBehavior**: only for `ICommand<TResponse>`; queries skip it. It owns the transaction, commit, and event dispatch phases (§4).
   One exception (Phase 5, D-42 Q1): a command marked `IHandlesOwnUnitOfWork` (the document upload) is let through, and its handler calls
   `IUnitOfWork` once itself, because the blob upload must happen outside the transaction (§5.1).

Not a MediatR behaviour:
- **Idempotency** is an ASP.NET resource filter (`IdempotencyFilter`), because it stores an HTTP response (D-24). It runs before model
  binding (to hash the raw body, or a form by its content, D-42 item 18) and wraps result execution (to capture the bytes sent). Only 2xx
  responses are stored (D-40 item 12).
- **Lenient JSON converters** (unknown enum → undefined, unparseable optional date → null) keep binding from rejecting values, so the
  validator words every 422 as FRS §8 does (D-40 item 3).
- **Authorization** by role is an endpoint policy, plus domain checks for data-dependent authority (D-25).

### 5.1 Document upload flow (Phase 5, D-42)
```
POST /api/claims/{id}/documents (multipart, ≤ 51 MB body; larger → 413 before reading)
  └─ ClaimDocumentsController → ISender.Send(UploadClaimDocumentCommand)       [IHandlesOwnUnitOfWork]
       ├─ LoggingBehavior → ValidationBehavior (file present, 1 B–50 MB, name/notes length, type)
       └─ UploadClaimDocumentCommandHandler
            1. SanitisedFileName (NFKC, last segment, invisible chars)  → DocumentFormat.Resolve (extension allowlist, declared type)
               → DocumentContentInspector (magic bytes / OOXML directory / no binary bytes)          ── 422 "File", nothing stored
            2. IClaimQueries.GetStatusAsync: missing/foreign → 404, Closed/Withdrawn → 422             ── nothing stored
            3. IStorageService.UploadAsync({org}/{claim}/{docId}_{name}, canonical type, no overwrite)  ── outside any transaction
            4. IUnitOfWork: load Claim → Claim.AddDocument (re-checks read-only, path belongs to claim)
                 → before commit: DOCUMENT_UPLOADED audit → SaveChanges → COMMIT
            5. on failure in 4: row exists? keep blob : IStorageService.DeleteAsync                   ── then rethrow
            6. sign a 1-hour download URL → 201 DocumentDto
GET /documents, GET /documents/{docId}/url → one SQL read → URLs signed locally (SAS, or HMAC token for the local fallback)
Browser → SAS URL → Blob Storage (the API never serves the bytes; the local fallback's dev-only endpoint is the documented exception)
```

---

## 6. Hangfire design (FRS §12, Brief §3.5) — revised in Phase 4 (D-41)

| Job | Trigger | Idempotency / safety | Failure |
|---|---|---|---|
| `PostGLReserveChangeJob(reserveHistoryId, claimId, idempotencyKey)` | after-commit enqueue on ReserveAutoApproved / ReserveApproved / GlPostingRetryRequested; the sweeper | One transaction (the command's unit of work): `UPDATE ReserveHistory SET PostingStatus='Posted', PostingJobId=@job WHERE Id=@id AND ClaimId=@claim AND IdempotencyKey=@key AND PostingStatus='Pending' AND ApprovalStatus IN ('Approved','AutoApproved')`; only if **exactly 1 row** changed: call the (simulated) ledger with the key, stage GL_POSTING_SIMULATED, commit. 0 rows → no-op. Never check-then-act. Backstop: unique filtered index on `ClaimAuditLog(RelatedEntityId) WHERE EventType='GL_POSTING_SIMULATED'`. | `[AutomaticRetry(Attempts = 3)]`, delays 10 s / 30 s / 60 s. On the final attempt: a new unit of work sets `Failed` (same compare-and-set: `WHERE PostingStatus='Pending'`) and writes GL_POSTING_FAILED with the reason, then the exception is rethrown so Hangfire shows the job as Failed (D-41 amends D-35). Recovery: `POST …/retry-posting` (Failed → Pending, audited, re-enqueued) |
| `SlaMonitoringJob` | recurring `*/15 * * * *`, registered at startup | Per organisation (D-31): Draft/Open claims with `COALESCE(UpdatedAt, CreatedAt) < now − 48h` and no SLA_BREACH_DETECTED in the last 24h get one audit row each; never touches Claims (D-01). `[DisableConcurrentExecution]` so two runs never overlap | default retries; the next run re-evaluates anyway |
| `GlPostingSweeperJob` | recurring every 5 min | Per organisation: re-enqueues approved rows with `PostingStatus='Pending'` and `ApprovedAt < now − 5 min` (at most 100 per run); safe because the GL job is idempotent (D-15) | default retries |
| `IdempotencyCleanupJob` | recurring daily | deletes IdempotencyRecords older than 24h (D-24) | — |

- Hangfire lives in Infrastructure; Application sees only `IBackgroundJobScheduler`. The job classes are thin: they open a DI scope, set the tenant
  (D-31) and a fresh correlation id, and send an Application command, so the business steps run through the same pipeline (logging, unit of
  work) as HTTP commands.
- Each job execution has its own DI scope, correlation id (in the log scope and on every audit row it writes) and tenant scope. The system actor
  writes `CreatedByUserId = null` (D-33).
- Time comes from `TimeProvider` everywhere, which makes the 48h / 24h / 5 min rules testable with `FakeTimeProvider`.
- The Hangfire dashboard is at `/hangfire`, **Manager role only**, and **read-only** (D-41).

### 6.1 Race conditions we defend against (Phase 4)

Written before the Phase 4 code, as the prompt requires. "Loser" is the request that commits second.

**Mechanisms (named once, referenced below).**
- **M1 — optimistic concurrency on the aggregate.** Every command that changes anything inside a claim also touches the `Claims` row (the
  `AuditColumnsInterceptor` marks `UpdatedAt` modified), so EF adds `WHERE RowVer = @original` to the claim UPDATE. A reserve change also
  updates its component, which has its own RowVer. On SQL Server the loser's UPDATE waits for the winner's row lock, then matches 0 rows →
  `DbUpdateConcurrencyException` → **409**. The loser's transaction rolls back with its audit rows, and nothing was enqueued, because
  enqueueing happens after commit.
- **M2 — unique indexes as the last word.** `UX (ReserveComponentId, ChangeSequence)`, `UX IdempotencyKey`, `UX ReserveComponentId WHERE
  PendingApproval` (D-22). A duplicate-key error at commit is also a lost race, so the unit of work maps SQL errors 2601/2627 to **409** as
  well (otherwise it would be a 500).
- **M3 — compare-and-set in one statement.** A background writer never reads a status and then writes it. It issues one conditional UPDATE and
  acts only on "exactly 1 row changed", inside the transaction that also writes the audit row.
- **M4 — after-commit side effects + the sweeper** (D-15).

| # | Race | What would go wrong without a defence | Defence | Proven by |
|---|---|---|---|---|
| R1 | Two approvers approve the **same** pending transaction at the same moment (also approve vs reject, approve vs retract) | Both see PendingApproval; both write APPROVED audit rows; two GL jobs; CurrentAmount counted once or twice depending on timing | M1: component + claim RowVer; loser → 409, no audit row, no job. A retry by the loser reloads and gets 422 "only a pending transaction can be decided" | `RSV_07_Concurrent_approvals_one_wins` (both requests forced to load before either writes) |
| R2 | Two adjustments on the **same component** at once | Both read `LastChangeSequence = n` and `CurrentAmount = x`; both insert sequence n+1 with PreviousBalance x: a forked balance chain and a duplicate GL key | M1 (component RowVer) and M2 (`UX (ReserveComponentId, ChangeSequence)`, `UX IdempotencyKey`, one pending per component). Whichever statement runs first, the loser gets 409 | `RSV_05_Concurrent_submissions_on_same_component_one_gets_409` |
| R3 | **Write skew** on the $10M limit: two approvals on **different components** of one claim (e.g. $6M Indemnity + $6M Expense with $0 approved) | Each approval checks 0 + 6M ≤ 10M against its own snapshot; they update different component rows, so component RowVers never collide: the claim ends at $12M | M1 on the **claim** row: both commands touch `Claims`, so the second conflicts → 409; its retry re-checks the limit and gets 422. The same argument covers approve vs "disable override" and approve vs "close claim" (CC-01) | `BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit` |
| R4 | The process dies **between COMMIT and enqueue** (or Hangfire storage is briefly unavailable) | An approved transaction stays `PostingStatus = Pending` forever | M4: `GlPostingSweeperJob` re-enqueues approved/Pending rows older than 5 minutes. A duplicate enqueue is harmless (R6) | `JOB_11_Sweeper_enqueues_stranded_postings` |
| R5 | A job runs **before the approval commits**, or for an approval that **rolls back** | The job finds nothing (or posts something that never happened) | M4: enqueue is an after-commit handler; a rolled-back unit dispatches nothing | `RSV_03_Approval_enqueues_gl_job_after_commit`; the loser in R1 enqueues nothing |
| R6 | The GL job runs **twice** for one transaction: a retry after an unclear failure, the sweeper + the original enqueue, a worker killed by scale-in and re-fetched, two workers **concurrently** | Check-then-act ("is it Posted? no → write audit, set Posted") lets both runs pass the check: two GL_POSTING_SIMULATED rows, two ledger postings | M3: `UPDATE … WHERE PostingStatus = 'Pending'` claims the row. A concurrent second run blocks on the row lock, then re-evaluates the predicate against the committed row → 0 rows → no-op. Sequential repeats also get 0 rows. The audit row is written in the same transaction, only after "1 row". Backstop: unique filtered index on `ClaimAuditLog(RelatedEntityId) WHERE EventType = 'GL_POSTING_SIMULATED'` | `BR_R_06_Job_run_twice_writes_one_gl_audit_entry`; `BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry` (the second run is observed **blocked** on the first run's lock before the first commits) |
| R7 | **Partial failure inside one run**: the status is updated but the audit insert (or the ledger call) fails | Posted with no audit row, or an audit row for a posting that did not happen; a retry then skips it | The UPDATE, the ledger call and the audit insert are one transaction; any failure rolls all of it back to Pending, and the retry starts clean | `JOB_03_Retry_after_failure_writes_single_entry` |
| R8 | The **final failing attempt** races a successful run of the same transaction (e.g. the sweeper's duplicate) | "Failed" overwrites "Posted", plus a GL_POSTING_FAILED for a transaction that was posted | M3 again: `SET Failed WHERE PostingStatus = 'Pending'`; GL_POSTING_FAILED is written only if that changed 1 row | `JOB_06_Final_attempt_failure_marks_failed_and_audits`; `JOB_06_Failure_never_overwrites_a_posted_transaction` |
| R9 | A **manual retry** (Failed → Pending) races another retry, or a stale job | Two users re-enqueue twice (harmless, R6), or a retry overwrites a status that changed after it was read | The retry goes through the aggregate (M1 on the claim). `PostingStatus` is also an EF **concurrency token**, so the retry's UPDATE is itself a compare-and-set (`WHERE PostingStatus = 'Failed'`). Jobs only post `Pending` rows (not `<> 'Posted'`), so a stray job can never post a Failed row behind the user's back: the only way back is the audited retry | `API_25_Retry_failed_posting`; `JOB_02_A_failed_posting_is_not_posted_by_a_stray_job` |
| R10 | The GL job and a user **edit the same claim** at the same time | If the job touched the claim row, the user would get a spurious 409, and the SLA clock (UpdatedAt) would be reset by a system action | The job updates only its ReserveHistory row, with `ExecuteUpdate` (no tracked claim, so no claim touch). Users never update an approved row except the retry, which needs `Failed` (disjoint from the job's `Pending`) | `JOB_05_Success_sets_posted_and_job_id` asserts the claim's RowVer and UpdatedAt are unchanged |
| R11 | Two **SLA runs overlap** (a run longer than 15 minutes, or two replicas) | Both see "no breach in the last 24h" for the same claim and both insert SLA_BREACH_DETECTED | `[DisableConcurrentExecution]`: a Hangfire distributed lock (an `sp_getapplock` in SQL storage) around the run. Recurring triggers fire each occurrence once across servers | `JOB_09_Second_run_within_24h_adds_nothing` (sequential); overlap is Hangfire's lock, documented rather than re-tested |
| R12 | The SLA job flags a claim **while a user updates it** | — | Benign by design: the job never writes Claims (no conflict, no clock reset). At worst one breach entry lands moments after an update; `isSlaBreached` compares timestamps (D-01) and the next run applies the rule again | `JOB_10_Sla_job_does_not_modify_claim_row` |
| R13 | A **transient SQL error** mid-unit (serverless resume, deadlock victim 1205) | Half-done work, or an enqueue for a unit that is then replayed | The execution strategy replays the whole unit from a clean change tracker; after-commit dispatch happens once, outside the replayed block (ARCHITECTURE-PLAN §4) | Phase 3 `CONV_15_After_commit_handlers_run_after_the_commit_and_never_after_a_rollback` |

**Isolation level.** SQL Server 2022 in Docker runs locking READ COMMITTED; Azure SQL has READ_COMMITTED_SNAPSHOT on. M1–M3 hold under both:
an UPDATE always takes an exclusive row lock and evaluates its WHERE clause against the latest *committed* row after waiting, it never updates
a stale snapshot row (that would be SNAPSHOT isolation, which fails with error 3960 instead). The tests run on the former; the argument covers the
latter.

**Races handled in earlier phases (for completeness).** Gap-free claim numbers (D-10: counter row lock inside the creating transaction);
duplicate `Idempotency-Key` requests (D-24: placeholder row + unique index, in-flight → 409).

---

## 7. Azure target architecture (Brief §2.3, §3.8; D-36)

```
 Browser ──► Azure Static Web App (Free)  — Angular build; env config holds the API URL
    │
    └──► Azure Container Apps (Consumption) — container `claims-api` (ASP.NET Core API + in-process Hangfire server)
            │  ingress: external HTTPS · scale: HTTP rule, minReplicas 0, maxReplicas 1
            │  image: ghcr.io/<owner>/claims-api:<sha> · system-assigned Managed Identity
            ├──► Azure SQL Database (serverless, free offer, auto-pause) — app schema + [HangFire] schema
            ├──► Storage account (Standard LRS) — container `claim-documents`
            │       SAS = user-delegation SAS signed with the MI (no account key in config)
            ├──► Key Vault — Auth:SigningKey, SQL connection string (ACA secrets as Key Vault references)
            └──► Application Insights (optional) — logs/traces with the correlation id
 Browser ──► SAS URL ──► Blob (download goes directly to Storage, never through the API — BR-D-02)
```
- **Why Container Apps (D-36):** scaling to zero stops the Hangfire server's SQL polling, which lets serverless SQL auto-pause. The idle cost is about USD 0.
  Trade-offs: cold start (container start + database resume, covered by EF connection resiliency), and the SLA recurring job runs only while a replica is up (Hangfire
  fires the missed occurrence on wake; the rule is state-based, so only the detection time shifts).
- **Live-review runbook:** before the session, `az containerapp update --min-replicas 1` and warm the database with one request. Revert to 0 afterwards.
- **Container image:** multi-stage Dockerfile (`sdk:9.0` build → `aspnet:9.0` runtime, non-root user, port 8080). No secrets baked in.
- **RBAC** for the MI: blob data access plus the right to generate user-delegation keys on the storage account, and Key Vault Secrets User. The exact role set
  (Storage Blob Data Contributor vs adding Storage Blob Delegator) is verified in Phase 7 and not asserted here.
- **Local dev:** `docker-compose.yml` with SQL Server 2022 + Azurite + the API (the same Dockerfile as production). The connection string gives an account-key
  SAS, and `Storage:Provider=AzureBlob` against Azurite *or* `LocalFileSystem`.
- **CORS:** locked to the SWA origin. **Swagger** is enabled in the deployed environment (Brief §4.3).
- **Probes:** liveness `/health/live` (no dependencies, so a paused database never restarts the container); readiness `/health/ready` (database) (D-38).
- **CI/CD** (GitHub Actions, `workflow_dispatch` is acceptable per Brief §3.8): build + test → `docker build` + push to ghcr.io → EF migrations bundle run against
  Azure SQL → `az containerapp update --image …:<sha>` → `ng build` → deploy the SWA. Uses OIDC federated credentials for `azure/login`.

---

## 8. Phased implementation plan (maps to Brief §5.2)

| Phase (PROMPTS.md) | Brief §5.2 phase | Content | Estimate |
|---|---|---|---|
| 0 Analysis | 1 Architecture & Design | this document, the matrix, decisions | 0.5 d |
| 1 Skeleton + cross-cutting | 3 Backend | projects, CPM, middleware, behaviours, auth, Dockerfile, docker-compose; **plus a walking-skeleton deploy** (API health on Container Apps + SWA hello) to de-risk Azure early | 1 d |
| 2 Domain + persistence | 2 Database Schema | aggregate, VOs, events, EF configs, interceptors, counter, seed, InitialCreate, domain tests | 1 d |
| 3 FNOL / claims / reference | 3 Backend | commands/queries/controllers, validators, HTTP integration tests | 1 d |
| 4 Reserves + Hangfire | 3 Backend | reserve commands, concurrency tests, GL/SLA/sweeper jobs | 1 d |
| 5 Documents | 3 Backend | storage abstraction, Azure + local, Azurite tests | 0.5 d |
| 6 Angular | 4 Frontend | list, FNOL, detail (5 tabs), auth, theming | 1.5–2 d |
| 7 Azure + CI/CD | 5 Deployment | Bicep/CLI (ACA env + app, SQL serverless, Storage, KV, SWA), pipeline, smoke script | 0.5 d |
| 8 Review + docs | 5 Review | matrix walk, README, ARCHITECTURE, AI-WORKFLOW, REVIEW-PREP | 0.5 d |
| **Total** | | | **≈ 7.5–8 d** |

This is above the 5–7 day timebox (Brief §8.1). Mitigations follow the Brief §8.3 priority (backend correctness → deployment → docs → frontend polish):
deploy a walking skeleton in Phase 1, keep Phase 6 to the required elements before any polish, and keep optional items (waive, Key Vault) cuttable.

## 9. Frontend (Phase 6, D-43)

Angular 22, standalone components, zoneless, Angular Material 22 (M3 theme from `#1B4F72`), Reactive Forms, Vitest. Source: `web/claims-ui/src/app`.

```
core/        the only layer that talks HTTP (ESLint rule): ClaimsApiService, ReferenceApiService, AuthService;
             interceptors (Bearer + correlation id → loading → ProblemDetails/snackbar); models mirroring the DTOs; CLOCK token
shared/      domain/  pure mirrors of domain rules the UI previews: authority tiers, policy period, intake warnings,
                      transitions + CC-01..04 pre-flight, reserve row actions, Add Reserve blockers
             forms/   validators with the API's messages, 422 → control mapping, party / risk-object / policy-typeahead controls
             ui/      status chip (FRS §11.1 colours), badges, authority indicator, dialogs, empty state, amounts
features/    claims-list/   ┐
             fnol-intake/   ├ one lazily loaded route file each → three chunks (D-17)
             claim-detail/  ┘ ClaimDetailStore (provided by the page), header + transition dialog, five tabs, Add Reserve drawer
```

- **Where the rules live.** The API is authoritative for every rule. The UI mirrors the ones it must *preview* (authority tier while typing, in-force
  badge, pre-flight checklist, which buttons a role sees) as pure functions in `shared/domain`, unit-tested against the same boundaries as the domain
  tests. Everything else is shown from the API's 422 body.
- **Data flow.** List: URL query params → `switchMap` → page. FNOL: one typed `FormGroup` per stepper step; derived values are `computed` over
  `form.events`. Detail: commands go through the API services; the store then reloads what changed; a 409 reloads the claim.
- **Identity.** A real signed JWT from `POST /api/auth/dev-token` (D-16), kept in sessionStorage per tab; the interceptor adds it only to API calls.
