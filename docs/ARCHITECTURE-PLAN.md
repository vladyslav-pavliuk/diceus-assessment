# Architecture plan (draft → becomes ARCHITECTURE.md in Phase 8)

Status: **DRAFT, Phase 0**. Based on the entries in `docs/DECISIONS.md`, all ACCEPTED on 2026-09-27 (D-36 changed to Container Apps).
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

Loading: the handler loads the Claim with its components. It loads only the reserve transactions it needs (the target transaction, or the pending ones
for closure), because the component stores the `CurrentAmount` projection and `LastChangeSequence`. Full history is never needed for a write.

### 2.3 Value objects
| VO | Purpose / invariant |
|---|---|
| `ClaimNumber` | `CLM-{YYYY}-{0000000}` format/parse (BR-C-04) |
| `Money` | decimal amount; currency implicit USD (NOT SPECIFIED, D-33). The seam for multi-currency |
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

**Why the split.** Audit rows must commit or roll back *with* the state change (no divergence), so they are written before commit. A Hangfire job must
never see uncommitted rows, and must not run for a transaction that rolls back, so it is enqueued after commit. The window between commit and enqueue is
closed by the GL sweeper (D-15). The jobs themselves write audit rows directly through `IAuditLogService`, because they do not go through an aggregate.

---

## 3. Data model

Legend: **SD** soft delete (IsDeleted, DeletedAt + filter) · **T** OrganisationId + tenant filter · **RV** RowVer · **AC** audit columns
(CreatedAt, UpdatedAt, UserCreated, UserModified). All PKs are `UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()`, with values assigned in the domain (D-30).

| Table | Key columns | FKs | Indexes | SD | T | RV | AC |
|---|---|---|---|---|---|---|---|
| Organisations | Id, Name | — | — | ✓ | — (is the tenant) | — | ✓ |
| Users | Id, Username, DisplayName, Role, IsActive | — | UX (OrganisationId, Username) | ✓ | ✓ | — | ✓ |
| Policies | Id, PolicyNumber, ClientName, EffectiveDate DATE, ExpirationDate DATE, Status, CoverageTypes (JSON) | — | UX (OrganisationId, PolicyNumber); IX ClientName | ✓ | ✓ | — | ✓ |
| CauseOfLossCodes | Id, Code, Name, PerilCategory, IsActive, SortOrder | — | UX (OrganisationId, Code) | ✓ | ✓ | — | ✓ |
| ClaimStatusTransitions | Id, FromStatus, ToStatus, MinimumRole, RequiresReason, IsSystemOnly | — | UX (FromStatus, ToStatus) | — | — (global) | — | ✓ |
| ClaimNumberCounters | (OrganisationId, Year) PK, LastValue INT | — | PK | — | n/a | — | — |
| **Claims** | Id, ClaimNumber, PolicyId?, PolicyNumber?, ClientName?, Status, Severity, ReportedDate, AssignedHandlerId?, ClosedAt?, ClosureReason?, Notes?, ReserveLimitOverride BIT, ReserveLimitOverrideByUserId?, ReserveLimitOverrideAt? | Policies, Users | UX (OrganisationId, ClaimNumber); IX (OrganisationId, Status, UpdatedAt) for list+SLA; IX AssignedHandlerId; IX PolicyId | ✓ | ✓ | ✓ | ✓ |
| LossEvents | Id, ClaimId, LossDate, LossDescription, LossLocation?, CauseOfLossCode, EstimatedLossAmount? DECIMAL(19,4), ReportDate, PoliceReportNumber? | Claims; CauseOfLossCodes(OrganisationId, Code) alternate key | UX ClaimId; IX LossDate; IX CauseOfLossCode | ✓ | ✓ | — | ✓ |
| ClaimParties | Id, ClaimId, PartyRole, PartyType, FirstName?, LastName?, CompanyName?, Email?, Phone?, Notes?, IsActive | Claims | IX (ClaimId, PartyRole, IsActive) | ✓ | ✓ | — | ✓ |
| ClaimRiskObjects | Id, ClaimId, AssetType, AssetDescription, DamageDescription?, IsPrimary, AssetReference? | Claims | IX ClaimId | ✓ | ✓ | — | ✓ |
| ClaimValidationIssues | Id, ClaimId, RuleCode, Severity, Field, Message, Status, RaisedAt, ResolvedAt?, ResolvedByUserId?, ResolutionNote? | Claims | UX (ClaimId, RuleCode) WHERE Status='Open' AND IsDeleted=0 | ✓ | ✓ | — | ✓ |
| **ClaimReserveComponents** | Id, ClaimId, Component, CurrentAmount DECIMAL(19,4), LastChangeSequence INT, Status, Notes? | Claims | UX (ClaimId, Component) WHERE IsDeleted=0 | ✓ | ✓ | ✓ | ✓ |
| ReserveHistory | Id, ReserveComponentId, ClaimId, TransactionType, Amount, PreviousBalance, NewBalance, ApprovalStatus, RequiredAuthority, ApprovedByUserId?, ApprovedAt?, RejectedByUserId?, RejectedAt?, RejectionReason?, ChangeReason, PostingStatus, PostingJobId?, IdempotencyKey?, ChangeSequence, SubmittedByUserId, LimitWarning BIT | Components, Claims | UX (ReserveComponentId, ChangeSequence); UX IdempotencyKey WHERE NOT NULL; UX ReserveComponentId WHERE ApprovalStatus='PendingApproval' (D-22); IX (ClaimId, CreatedAt); IX (ApprovalStatus, PostingStatus, ApprovedAt) for the sweeper | ✓ | ✓ | — | ✓ (amount columns guarded, D-22) |
| ClaimDocuments | Id, ClaimId, DocumentType, DocumentName, BlobPath, ContentType, FileSizeBytes BIGINT, UploadedAt, UploadedByUserId?, Notes? | Claims | IX ClaimId | ✓ | ✓ | — | ✓ |
| **ClaimAuditLog** | Id, ClaimId, EventType, Description, OldValue?, NewValue?, RelatedEntityId?, RelatedEntityType?, CorrelationId?, CreatedAt, CreatedByUserId? | Claims | IX (ClaimId, CreatedAt DESC); IX (ClaimId, EventType, CreatedAt) for SLA dedupe | **—** (D-14) | ✓ | — | CreatedAt/CreatedByUserId only |
| IdempotencyRecords | Id, UserId, Key, Method, Route, RequestHash, StatusCode?, ResponseBody?, CreatedAt, CompletedAt? | — | UX (UserId, Key) | — | ✓ | — | CreatedAt only |
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
 → ExceptionHandlingMiddleware    maps exceptions → ProblemDetails (422/404/403/409)
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

---

## 5. MediatR pipeline behaviours (order)
1. **LoggingBehavior** (outermost): times and logs everything, including validation failures.
2. **ValidationBehavior**: runs all `IValidator<TRequest>`, aggregates the errors, and throws `ValidationException` → 422 with the FRS §10.4 shape. It runs
   before any transaction is opened.
3. **UnitOfWorkBehavior**: only for `ICommand<TResponse>`; queries skip it. It owns the transaction, commit, and event dispatch phases (§4).

Not a MediatR behaviour:
- **Idempotency** is an ASP.NET filter, because it stores an HTTP response (D-24).
- **Authorization** by role is an endpoint policy, plus domain checks for data-dependent authority (D-25).

---

## 6. Hangfire design (FRS §12, Brief §3.5)

| Job | Trigger | Idempotency / safety | Failure |
|---|---|---|---|
| `PostGLReserveChangeJob(reserveHistoryId, claimId, idempotencyKey)` | after-commit enqueue on AutoApproved/Approved; retry endpoint; sweeper | One transaction: `UPDATE ReserveHistory SET PostingStatus='Posted', PostingJobId=@job WHERE Id=@id AND IdempotencyKey=@key AND PostingStatus<>'Posted'`, then write GL_POSTING_SIMULATED **only if @@ROWCOUNT = 1**, then commit. A concurrent duplicate blocks on the row lock, then updates 0 rows → no-op. The unique IdempotencyKey index is the backstop. Never check-then-act. | `[AutomaticRetry(Attempts = 3)]`. On the final attempt: catch, set PostingStatus='Failed' (conditional on not Posted), write GL_POSTING_FAILED with the reason, swallow (D-35) |
| `SlaMonitoringJob` | recurring `*/15 * * * *`, registered at startup (`RecurringJob.AddOrUpdate`) | Per organisation (D-31): select Draft/Open claims with `COALESCE(UpdatedAt, CreatedAt) < now-48h` and NOT EXISTS an SLA_BREACH_DETECTED row in the last 24h; insert one audit row each; never touches Claims (D-01) | default retries; the next run re-evaluates anyway |
| `GlPostingSweeperJob` | recurring every 5 min | re-enqueues Approved/AutoApproved rows with PostingStatus=Pending and ApprovedAt < now-5 min; safe because the GL job is idempotent (D-15) | default retries |
| `IdempotencyCleanupJob` | recurring daily | deletes IdempotencyRecords older than 24h (D-24) | — |

- Jobs are resolved from DI through the `IBackgroundJobScheduler` adapter (in Application). Only Infrastructure references Hangfire.
- Each job execution creates its own DI scope, correlation id and tenant scope.
- Time comes from `TimeProvider` everywhere, which makes the 48h and 24h rules testable with `FakeTimeProvider`.
- The Hangfire dashboard is enabled in Development, and in Azure only behind an authorization filter (Manager role via a cookie issued from the dev token, or basic auth). Decided in Phase 4.

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
