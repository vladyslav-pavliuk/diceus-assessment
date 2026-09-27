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
`Money` becomes (Amount, Currency) with a Currency column on ReserveHistory and components. Authority thresholds and the $10M limit are defined in a base
currency, with an FX rate snapshot stored on each transaction (rate, rate date), so history stays reproducible. Components are keyed by (type, currency),
and the GL journal carries both amounts. Validation rejects mixed-currency adjustments on one component. (D-33)

**Why is "waive" not implemented?**
Under D-06, the only Critical issue stored on a claim is "no claimant". §4.2 and BR-ST-02 separately require a claimant, so waiving that issue could never
unblock anything. (D-07)
