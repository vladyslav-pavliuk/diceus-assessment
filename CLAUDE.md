# CLAUDE.md — DICEUS Claims Module (FNOL + Reserve Management)

Persistent context for every Claude Code session in this repo. Read this first. The full sources are in
`docs/spec/claims-frs.md` (the FRS, the **detailed** source of truth) and `docs/spec/assessment-brief.md`
(the assessment brief: stack, architecture, deliverables, evaluation). When they conflict, follow
`docs/DECISIONS.md`. If a conflict is not yet listed there, **stop and ask**. Do not choose silently.

## What this is
A greenfield vertical slice of the Claims module of an insurance Policy Administration System (PAS),
built as a senior full-stack technical assessment for DICEUS. Scope: FNOL intake, claim status state machine,
parties and risk objects, event-sourced reserves with a 3-tier approval authority, Hangfire GL-posting simulation,
an SLA monitoring job, document upload (Azure Blob with local fallback), and an append-only audit log. There is also an
Angular frontend. It is deployed to Azure with a CI/CD pipeline.

Reviewers weight: Engineering quality & architecture 35% · AI-assisted delivery 25% · Fullstack delivery 25% ·
Domain correctness 15%. They care more about **correctness, consistency and explainable decisions** than
about volume. The author (Vlad) must be able to defend every line in a 90-minute live review, so prefer
clear, conventional code over clever code.

## Stack (fixed by the brief)
.NET 9 / C# 13 · ASP.NET Core Web API · MediatR 12.x · EF Core 9 · FluentValidation · AutoMapper · Hangfire
(SQL Server storage) · SQL Server 2022 / Azure SQL · Azure Blob Storage · Angular 18+ (standalone APIs,
signals OK) · Angular Material · Reactive Forms · GitHub Actions · Azure Container Apps (API, scale to zero) + Static Web App (see DECISIONS.md D-36).
- Pin **MediatR to 12.x** (Apache-2.0) and **AutoMapper to 14.x** (MIT), the last freely licensed majors, because newer majors need a
  commercial licence key. Verify the versions on NuGet before adding them and record the choice in DECISIONS.md.
- For tests, use xUnit and Testcontainers (MsSql) for integration tests. Use Shouldly or FluentAssertions 7.x for
  assertions (v8+ is commercial).

## Solution layout (required by the brief)
```
src/ClaimsModule.Domain          entities, value objects, enums, domain events, domain exceptions — no deps
src/ClaimsModule.Application     MediatR commands/queries + handlers, validators, DTOs, AutoMapper profiles,
                                 interfaces (IUnitOfWork, repos, IStorageService, IAuditLogService,
                                 ICurrentUser, IBackgroundJobScheduler), pipeline behaviours
src/ClaimsModule.Infrastructure  Azure Blob + LocalFileSystem storage, Hangfire jobs + scheduler adapter,
                                 auth/token services, TimeProvider usage
src/ClaimsModule.Persistence     DbContext, IEntityTypeConfiguration<T> classes, migrations, seed (HasData),
                                 repositories, UnitOfWork, SaveChanges interceptors
src/ClaimsModule.API             controllers, middleware (exception → ProblemDetails, correlation id),
                                 DI composition, Swagger, Hangfire dashboard
tests/ClaimsModule.Domain.Tests, tests/ClaimsModule.Application.Tests, tests/ClaimsModule.IntegrationTests
web/claims-ui                    Angular app
docs/                            spec/, DECISIONS.md, ai-log/, REVIEW-PREP.md
```
Dependency rule: Domain ← Application ← (Infrastructure, Persistence) ← API. Application never references EF Core
types or Azure SDKs. AutoMapper profiles live in Application.

## Non-negotiable engineering rules
1. **CQRS via MediatR**: every state change is a `VerbNounCommand`. Every read is a `GetNounQuery` or `ListNounsQuery`.
   Controllers stay thin: bind the request, call `ISender`, map the result to HTTP. Put no business logic in controllers.
2. **FluentValidation runs in a MediatR `ValidationBehavior`**, not in controllers. Validation failures become
   HTTP 422 with the exact body shape from FRS §10.4 (`type`, `title`, `status`, `errors{field:[msgs]}`).
3. **Domain rules live in the domain.** The Claim aggregate owns the status transitions and the reserve-component
   invariants. Validators check request shape and reference data. Entities protect invariants. Nothing may set
   `Status` outside the aggregate.
4. **Unit of Work** coordinates each command in one transaction. Handlers never call `SaveChanges` directly.
5. **Domain events** are raised by aggregates (`ClaimCreated`, `ClaimStatusChanged`, `ReserveTransactionSubmitted`,
   `ReserveApproved`, `ReserveRejected`, `ReserveRetracted`, `PartyAdded`, `PartyRemoved`, `DocumentUploaded`, …).
   - Audit-log writes are dispatched **before commit**, in the same transaction, so audit and state cannot diverge.
   - Enqueueing Hangfire jobs happens **after commit**, so a job never sees uncommitted rows. Document this split.
6. **Audit log is append-only.** All writes go through `IAuditLogService`. A SaveChanges interceptor throws if any
   `ClaimAuditLog` entry is `Modified` or `Deleted`. Optionally add a DB trigger or DENY in a migration as
   defence-in-depth. CorrelationId comes from the request (middleware reads or creates `X-Correlation-Id`) and is
   stamped on every audit row in that request. Hangfire jobs use their own correlation id.
7. **Reserves are event-sourced.** Never UPDATE an amount. INSERT a `ReserveHistory` row. The component's
   `CurrentAmount` is a projection recalculated in the same transaction. `ChangeSequence` increases monotonically
   per component and is protected by the component's `RowVer`.
8. **Money** is `decimal` + `DECIMAL(19,4)` (`HasPrecision(19,4)`). Never use double or float.
9. **Time**: inject `TimeProvider`. Never call `DateTime.Now` or `DateTimeOffset.UtcNow` directly. Store everything as UTC `DATETIMEOFFSET(7)`.
10. **EF conventions** (FRS §15): Fluent API only (no data annotations for schema). GUID PKs with
    `HasDefaultValueSql("NEWSEQUENTIALID()")`. `RowVer` `IsRowVersion()` on Claims and ClaimReserveComponents.
    Soft-delete columns plus a global query filter. **Tenant `OrganisationId` on every business table plus a global query
    filter** driven by `ICurrentUser.OrganisationId`. Audit columns are filled by a SaveChanges interceptor. Enums are
    stored as NVARCHAR(50) via value conversion. Seed data uses `HasData` or migrations only, never startup code.
    `dotnet ef database update` must build a fresh DB from zero.
11. **No hard-coded GUIDs in application logic.** OrganisationId and user IDs come from config or seeded data.
12. **Errors**: one exception-handling middleware maps domain exceptions to ProblemDetails:
    `ValidationException→422`, `BusinessRuleViolation→422`, `NotFound→404`, `Forbidden→403`,
    `DbUpdateConcurrencyException→409`. Use structured logging (Serilog or built-in) with correlation id in scope.
13. **Idempotency-Key header** on write endpoints: store (key, user, route, response hash/status) and replay the stored
    response for a repeated key.

## Domain cheat-sheet (verify details in the FRS)
- **Statuses**: Draft, Open, UnderInvestigation, PendingPayment, Closed, Reopened, Withdrawn.
  The transitions from FRS §4.2 are seeded into a `ClaimStatusTransitions` table (From, To, RequiredRole, RequiresReason)
  and served at `GET /api/reference/claim-statuses`. Invalid transition → 422 listing the valid next statuses. Reopened
  moves to Open automatically, which writes two STATUS_CHANGED entries.
- **Closure (CC-01..04)**: no PendingApproval reserves, no unresolved Critical issues, at least one active Claimant.
  If any component has CurrentAmount > 0, return a warning and require an explicit justification note to confirm.
- **Authority** is based on the |amount| of a single transaction: ≤10k auto-approved (any role); >10k–≤100k needs
  Supervisor or Manager; >100k needs Manager. **No self-approval** (422 "Self-approval is not permitted.").
  PendingApproval rows are immutable: the submitter may *retract* them (status Cancelled). A rejected row stays in
  history and a resubmission creates a new row.
- **Aggregate limit**: approved reserves across all components may total at most $10,000,000 unless a Manager has set
  `ReserveLimitOverride` on the claim. Otherwise return a warning and block approval.
- **No policy linked** gives a Warning and blocks reserve creation until a policy is linked.
- **Claim number**: `CLM-{YYYY}-{0000000}`, gap-free, atomic. Use a counter table keyed by (OrganisationId, Year)
  and `UPDATE … OUTPUT INSERTED` **inside the claim-creation transaction**. A SQL SEQUENCE can leave gaps on rollback
  or cache loss, which breaks "no gaps". Explain this trade-off in DECISIONS.md.
- **GL job** `PostGLReserveChangeJob(reserveHistoryId, claimId, idempotencyKey)` needs a race-safe no-op.
  Use a conditional update (`… SET PostingStatus='Posted' WHERE Id=@id AND ClaimId=@claimId AND IdempotencyKey=@key
  AND PostingStatus='Pending' AND ApprovalStatus IN ('Approved','AutoApproved')`) and write the audit row only if exactly
  1 row changed, all in one transaction. `= 'Pending'`, not `<> 'Posted'`: a Failed posting is posted again only after the
  audited retry endpoint puts it back to Pending (D-41 Q1). Backstops: unique index on the idempotency key and a unique
  filtered index allowing one GL_POSTING_SIMULATED per transaction. Do **not** use a naive check-then-act. Retries:
  `[AutomaticRetry(Attempts = 3)]` (a constant); the last attempt sets PostingStatus=Failed (same compare-and-set on
  Pending) and writes GL_POSTING_FAILED in a new unit of work, then rethrows so Hangfire shows the job as Failed (D-41 Q2).
- **SLA job** `SlaMonitoringJob`, cron `*/15 * * * *`: finds Draft/Open claims with `UpdatedAt` older than 48h and
  writes SLA_BREACH_DETECTED at most once per 24h per claim. It does **not** change `Status`.
- **Documents**: `IStorageService` has `AzureBlobStorageService` (container `claim-documents`, path
  `{orgId}/{claimId}/{sanitisedFileName}`, 1h SAS) and `LocalFileSystemStorageService`. The provider comes from
  `Storage:Provider`. MIME allowlist: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV. Size limit 50 MB. Never stream bytes
  through the API for Azure.
- **Roles**: handler, supervisor, manager. The backend is authoritative. The frontend only hides buttons.

## Working agreement with Claude
- Work **phase by phase** as described in `docs/PROMPTS.md`. At the end of each phase, stop and give me a summary of what
  was built, the decisions made, any assumptions, and how to verify it. Wait for my go-ahead.
- Before writing code for a phase, list the FRS rule IDs (BR-*, CC-*, §x.y) that the phase covers. Name tests
  after those rule IDs (e.g. `BR_R_03_Self_approval_is_rejected`).
- **Never** resolve an ambiguity between the brief and the FRS silently. Propose options, recommend one, and add it to
  `docs/DECISIONS.md` (ID, context, options, decision, rationale).
- After every phase, append an entry to `docs/ai-log/phase-N.md`: the prompt I gave, what you generated, what I
  changed or rejected, and what you got wrong. When I correct you, record the before/after honestly. This feeds
  AI-WORKFLOW.md. Never invent corrections that did not happen.
- Add likely reviewer questions and concise answers to `docs/REVIEW-PREP.md` as you go (e.g. "Why a Command rather
  than a service call?", "What breaks without the UoW?", "How would you add multi-currency reserves?").
- Run `dotnet build`, `dotnet test`, and `ng build` / `ng test` before declaring a phase done. Do not claim that
  something works without running it.
- Keep the diffs of each phase reviewable. Commit per logical unit using conventional commit messages.
