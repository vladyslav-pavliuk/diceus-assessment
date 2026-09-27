# AI log: Phase 2 (domain model & schema)

- **Date:** 2026-09-27
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5.
- **Context loaded:** `CLAUDE.md`, `docs/DECISIONS.md` (re-read in full), `docs/PROMPTS.md`, `docs/ARCHITECTURE-PLAN.md`, `docs/REQUIREMENTS-MATRIX.md`,
  FRS §4, §5.3–5.6, §6, §7, §8, §9, §14, §15 (the full FRS was re-read), the Phase 1 code and tests.

## 1. Prompt given (verbatim: PHASE 2 from `docs/PROMPTS.md`)

```
Phase 2: domain model and schema. Re-read FRS §4, §6, §7, §9, §15 and DECISIONS.md.
1. Domain: the Claim aggregate (status state machine driven by the transition rules; closure conditions
   CC-01..04; parties; risk objects; validation issues), reserve components + ReserveHistory (event-sourced;
   authority evaluation; retract/approve/reject invariants; ChangeSequence; aggregate $10M check with override),
   value objects, domain events, and domain exceptions. The entities must protect their own invariants, with no public
   setters on state that rules govern.
2. Domain unit tests for EVERY business rule in the requirements matrix, named after the rule ID. Include edge
   cases: exactly $10,000 / $10,000.01 / $100,000 / $100,000.01; self-approval; supervisor approving >100k;
   PendingApproval modification; closing with open reserves without and with justification; Reopened→Open;
   removing the last active Claimant; SubrogationRecoverable negative amounts.
3. Persistence: DbContext, one IEntityTypeConfiguration<T> per entity, conventions from FRS §15 (NEWSEQUENTIALID,
   HasPrecision(19,4), DATETIMEOFFSET, IsRowVersion, enum→NVARCHAR(50) conversions, soft-delete + tenant global
   filters), audit-column and append-only-audit interceptors, the claim-number counter table, and HasData seed for
   policies (FRS §5.5 exactly), cause-of-loss codes (§5.6 exactly), status transitions, users, and the organisation.
   Create the InitialCreate migration.
4. An integration test (Testcontainers MsSql) that runs the migrations on a fresh DB, checks the seed data, and
   proves the claim-number generator produces unique, gap-free numbers under 50 parallel creates.
Before generating the migration, show me the entity/table mapping for review. Afterwards, show me the generated
migration's column types for Claims, ReserveHistory, and ClaimAuditLog so I can verify the conventions.
Stop and summarise, then update the matrix, the ai-log, and REVIEW-PREP.
```

Follow-up prompt after the mapping review (verbatim):
```
1. let's keep guid.
2. okay, sounds reasonable, approved.

also I approve D-39 also lists 18 smaller choices, will be adjusted later if needed.
```

## 2. Requirement IDs touched
BR-C-01..07, B-C-06, BR-R-01..06, BR-ST-01..04, CC-01..04, BR-P-01/02, PTY-01, BR-A-01, BR-D-01 (file-name VO only), TR-00..13, VAL-03/06/07/10..14,
RSV-01..06, RSV-08, SEC-04, CONV-01..08, CONV-10, CONV-11 (re-verified), CONV-15 (UoW part), API-CORR, AUD-02, AUD-I2/I3, new rows DOM-01, DOM-02.
Decisions applied: D-05, D-06, D-07, D-09, D-10, D-11, D-12, D-13, D-14, D-18, D-19, D-20, D-21, D-22, D-23, D-26, D-27, D-28, D-30, D-31, D-32, D-33, D-34;
new D-39 (accepted); D-38 item 10 amended.

## 3. What was generated
| Area | Files |
|---|---|
| Domain | `Claim` (4 partial files: creation/details/documents, status, parties/issues, reserves), `LossEvent`, `ClaimParty`, `ClaimRiskObject`, `ClaimValidationIssue`, `ClaimDocument`, `ClaimStatusTransition` + `StatusTransitionTable`, `ReserveComponent`, `ReserveTransaction`, `ReserveAuthorityPolicy`, `ReserveLimits`, `GlIdempotencyKey`, `ClaimNumber`, `SanitisedFileName`, `Policy`, `CauseOfLossCode`, `ClaimAuditLog`, `AuditEventTypes`, 20 domain events, `DomainMessages` (FRS §8 verbatim + marked assumptions), `ErrorKeys`, `AggregateRoot`, `SequentialGuid`, `RuleViolations`, `Actor`; `NotFoundException`/`ForbiddenAccessException` moved from Application |
| Application | `ITenantContext`, `IClaimNumberGenerator`, `IClaimRepository` |
| Infrastructure | `TenantContext` (JWT org by default, explicit for jobs) |
| Persistence | `ClaimsDbContext` (combined soft-delete + tenant filter per entity), `ModelBuilderConventions`, 14 `IEntityTypeConfiguration<T>` classes, `AuditColumnsInterceptor`, `ImmutableRowsInterceptor`, `ClaimNumberGenerator`, `ClaimNumberCounter`, `UnitOfWork`, `AuditLogService`, `ClaimRepository`, `SeedData`, regenerated `InitialCreate` (+ audit trigger and `claims_app` DENY role) |
| API | `CorrelationIdMiddleware` accepts only GUID ids (D-39 Q1) |
| Tests | Domain 264 (was 17), Integration 84 (was 55; +31 new persistence tests, 2 correlation tests changed) |
| Docs | D-39 (accepted), D-38 item 10 amendment, matrix statuses, ARCHITECTURE-PLAN §2.2/§2.3/§3, REVIEW-PREP Phase 2 section, this log |

**Verification that ran:**
- `dotnet build` + `dotnet test` on the **.NET 9 SDK 9.0.318** in `mcr.microsoft.com/dotnet/sdk:9.0` (Testcontainers SQL Server 2022 through the host Docker socket):
  0 warnings (warnings are errors), **360/360 pass** (264 domain, 12 application, 84 integration). Also on the host SDK 10 with `DOTNET_ROLL_FORWARD=Major`.
- `dotnet ef dbcontext script` before the migration existed, to review the DDL generated from the model (sent to Vlad with the mapping).
- **Mutation check** of the 50-parallel test: `IncrementAsync` was temporarily replaced with a read-then-write (SELECT, delay, UPDATE). The test failed
  with a duplicate `CLM-2091-0000006`, refused by `UX_Claims_OrganisationId_ClaimNumber`. The original was restored.
- Not re-run this phase: the docker-compose `app` profile smoke test (migrations bundle + API). `CONV_11` covers migrate-from-zero.

## 4. What Vlad decided during the phase
Claude stopped once, before generating the migration, with the entity/table mapping and two open questions (D-39):
- **Q1 `ClaimAuditLog.CorrelationId` type.** Options: keep GUID and accept only GUID client ids (recommended) / NVARCHAR(64) / GUID with NULL for
  non-GUID ids. **Vlad chose GUID** ("let's keep guid"). D-38 item 10 amended; the middleware and two Phase 1 tests changed.
- **Q2 load the whole aggregate** instead of "only the transactions needed" (ARCHITECTURE-PLAN §2.2). **Approved.** `IClaimRepository` was added so
  the integration tests exercise that path.
- **The 18 smaller D-39 choices** were accepted as written, "will be adjusted later if needed".

## 5. Where Claude refined the plan or the context files
- ARCHITECTURE-PLAN §2.2 (partial loading) → full load (Q2). §2.3 listed a `Money` value object; not built (D-39 item 12), and REVIEW-PREP's
  multi-currency answer was corrected, since it described a type that does not exist.
- D-23 said the idempotency key column is filtered `WHERE NOT NULL`; every row gets a key at submission, so it is NOT NULL and plainly unique.
- D-07's unique index covered `Status = 'Open'` only; it now covers Open and Acknowledged, otherwise re-validation would raise an acknowledged warning again.

## 6. What Claude got wrong (honest record, all caught before the phase ended)
1. **The most important one: store-generated keys.** The convention gave every `Id` a `NEWSEQUENTIALID()` default, which makes EF treat the key as
   store-generated. Because the domain assigns ids, EF then tracked a *new* reserve transaction added to a loaded claim as **Modified** (an UPDATE of a
   row that does not exist) instead of Added. `ImmutableRowsInterceptor` caught it in the first integration run ("attempted to change Amount, …"). Fixed
   with `ValueGeneratedNever()`, keeping the SQL default, and the migration was regenerated. Every "add a child to a loaded claim" command in Phase 3
   would otherwise have failed.
2. `AuditColumnsInterceptor` decided "has audit columns" by looking for `CreatedAt`. ClaimAuditLog has its own real `CreatedAt`, so writing an audit
   row crashed on the missing `UserCreated`. It now checks `UserCreated`.
3. The generated DDL had `RowVer rowversion NULL`; FRS §15.1 says NOT NULL. Caught while reviewing the script before showing the mapping; fixed with `IsRequired()`.
4. Collection properties first returned the backing `List<T>` itself, so a caller could cast and add. Claude's own `DOM_01` test caught it (fixed
   with `AsReadOnly()`). The first version of that test was itself wrong: it asserted the list is not an `ICollection<T>`, but `ReadOnlyCollection<T>`
   implements it. The test now asserts that `Add` throws.
5. An aggregate-limit test first submitted a Supervisor-tier (pending) subrogation and then another subrogation on the same component, which the
   one-pending rule forbids. Caught on self-review before the first run.
6. `AssignHandler` first had an unused `now` parameter silenced with `_ = now;`. The parameter was removed instead.
7. Test code: namespace clashes (`Reserves.`, `Documents.` resolved to the test namespaces), a Shouldly overload that does not exist
   (`ShouldHaveSingleItem(predicate)`), and a missing `using` for `GetService`. Compile errors, fixed.
8. The fixture helper found the seeded organisation with `Single()`, which broke once the tenant-isolation test inserted a second organisation into the
   shared database. It now looks the organisation up by name.
9. The first containerised test run redirected `obj`/`bin` to one shared path, which collides across projects. Redone by copying the sources into the container.
10. The script that updated the matrix also rewrote the section 1 brief→FRS mapping table, whose rows use the same IDs. Noticed in the diff and
    restored from git before committing.

## 7. What Vlad changed or rejected in review
- Chose GUID for the correlation id (Q1) and approved the full aggregate load (Q2), both as recommended. Nothing was rejected.
- _Further review comments to be added here. Do not invent entries._
