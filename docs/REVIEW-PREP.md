# Review prep: likely reviewer questions (Brief §7)

Short answers, each linked to the decision or section it comes from. This list grows every phase. Code references are added once the code exists.

## Architecture & CQRS
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

## Data & EF Core
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

## Hangfire
**How is the GL job idempotent under concurrent execution?**
It never checks and then acts. One transaction runs a conditional `UPDATE … WHERE PostingStatus <> 'Posted'` and writes the audit row only if exactly 1 row
changed. A concurrent duplicate blocks on the row lock, then updates 0 rows. A unique index on the idempotency key is the backstop. (ARCHITECTURE-PLAN §6)

**What if the process dies between commit and enqueue?**
The row still says `PostingStatus = Pending`. A 5-minute sweeper re-enqueues such rows, and the job's idempotency makes a double enqueue harmless. The
ReserveHistory row acts as the outbox. (D-15)

**Why doesn't the SLA job store a flag on the claim?**
Any write to Claims bumps `UpdatedAt`, and that would reset the 48h clock the job measures. The last breach is derived from the audit log, as FRS §12.2 says.
(D-01)

## Azure
**Why Container Apps and not App Service?**
Cost. Hangfire polls SQL all the time a server runs, so an always-on App Service keeps a serverless database awake and burns the free allowance. Container Apps
scales to zero when idle, the polling stops, and the database auto-pauses. The idle cost is about zero. The price is a cold start (container + database resume, absorbed
by EF connection retries), and SLA checks run only while a replica is up. The missed run fires on wake, and the rule is state-based, so only the detection time moves.
For the live review, `minReplicas` is set to 1. (D-36)

**What happens to a GL job if the container scales in mid-run?**
The job is persisted in SQL. After Hangfire's invisibility timeout, the next replica picks it up again. The conditional update makes a re-run a no-op if the first run
had already committed. (D-36, ARCHITECTURE-PLAN §6)

## Domain
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

## Phase 1: skeleton & cross-cutting
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
A client (the SPA, a gateway) can then tie its own logs to ours. The value lands in logs, audit rows and a response header, so only a short plain token
(1–64 of `[A-Za-z0-9-_.]`) is accepted. Anything else is replaced, not rejected: a tracing header should never fail a business request. (`CorrelationIdMiddleware.cs`)

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

## Phase 2: domain model & schema
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

## Phase 3: commands, queries, controllers
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
