# Decisions register — DICEUS Claims Module

Sources: **FRS** = `docs/spec/claims-frs.md` (detailed source of truth), **Brief** = `docs/spec/assessment-brief.md`
(authoritative on stack, layout, deliverables). Each entry has a Context, Options, Recommendation and Rationale.
**Status:** on 2026-09-27 Vlad accepted all entries as recommended ("apply the most logical and optimal options"), with one change: D-36 uses **Azure Container Apps** instead of App Service. Business rules may be adjusted after the first deployed version, and any change gets a new dated entry or amendment here.

Markers used below:
- **NOT SPECIFIED**: neither document says anything on the point.
- **ASSUMPTION**: my own choice where the documents are silent.
- **DEVIATION**: a conscious departure from the literal text of a spec, with the reason given.

## Index

| ID | Topic | Status |
|---|---|---|
| D-01 | SLA job must not change status; where "breached" is tracked | ACCEPTED |
| D-02 | Rule-ID numbering clash (FRS vs brief) | ACCEPTED |
| D-03 | Reserve component names | ACCEPTED |
| D-04 | Reserve adjust API shape (PUT vs POST + transactionType) | ACCEPTED |
| D-05 | Negative amounts, transaction types, authority sign | ACCEPTED |
| D-06 | Which intake criticals return 422 and which are persisted on a Draft | ACCEPTED |
| D-07 | ClaimValidationIssues entity; acknowledge/waive | ACCEPTED |
| D-08 | Endpoints implied but not listed | ACCEPTED |
| D-09 | Who may perform each status transition | ACCEPTED |
| D-10 | Gap-free claim number generation | ACCEPTED |
| D-11 | CurrentAmount definition; $10M aggregate check | ACCEPTED |
| D-12 | Tenant column name and scope | ACCEPTED |
| D-13 | ClaimType / CauseOfLossCode / LossDate on Claims | ACCEPTED |
| D-14 | Audit log vs soft-delete/audit-column conventions | ACCEPTED |
| D-15 | After-commit enqueue vs transactional outbox | ACCEPTED |
| D-16 | Mock authentication and seeded users | ACCEPTED |
| D-17 | Angular "lazy-loaded feature modules" with standalone APIs | ACCEPTED |
| D-18 | How and when AssignedHandlerId is set | ACCEPTED |
| D-19 | Do Warnings block Draft → Open? | ACCEPTED |
| D-20 | Transition table: brief path vs FRS table | ACCEPTED |
| D-21 | Approval status at component vs transaction level | ACCEPTED |
| D-22 | ReserveHistory mutability; balances on pending rows | ACCEPTED |
| D-23 | Meaning of `{ReserveId}` in the GL idempotency key | ACCEPTED |
| D-24 | HTTP Idempotency-Key header design | ACCEPTED |
| D-25 | 403 vs 422 for authority failures | ACCEPTED |
| D-26 | Closure/withdraw/reopen API shape and side effects | ACCEPTED |
| D-27 | ClaimParties: IsActive vs IsDeleted | ACCEPTED |
| D-28 | Document blob naming, local fallback download, size limit | ACCEPTED |
| D-29 | Claims-list API vs dashboard mismatches | ACCEPTED |
| D-30 | Primary-key generation (ids needed before SaveChanges) | ACCEPTED |
| D-31 | Tenant/soft-delete filters inside background jobs | ACCEPTED |
| D-32 | Date semantics (loss date vs policy dates, report dates) | ACCEPTED |
| D-33 | Minor gaps (severity, client name, coverage storage, system actor, …) | ACCEPTED |
| D-34 | Seeded policies expire around the review date | ACCEPTED |
| D-35 | Hangfire retry count and the exhausted-retries path | ACCEPTED |
| D-36 | Azure hosting: Container Apps (scale to zero) + serverless SQL | ACCEPTED |
| D-37 | Package versions and licences (+ AutoMapper CVE suppression) | ACCEPTED |
| D-38 | Phase 1 cross-cutting choices (errors, auth, shadow columns, health, toolchain) | PROPOSED |
| D-39 | Phase 2 domain and schema choices (+ two open questions) | ACCEPTED |
| D-40 | Phase 3 API, read-side and pipeline choices (+ two decisions by Vlad) | PROPOSED (Q1, Q2 ACCEPTED) |

---

## D-01 — SLA job: status vs flag
**Context.** Brief §3.5 says the job "flags them with a SlaBreached status". FRS §12.2 says: "Do not change claim status." `SlaBreached`
is not a status in FRS §4.1 or in brief Appendix A.1. FRS §12.2 also requires dedupe: "Track last breach event per claim; only add
a new entry if 24+ hours have passed since the last SLA_BREACH_DETECTED entry."

There are two hidden traps:
1. If the job writes any column on `Claims` (e.g. `IsSlaBreached`, `LastSlaBreachAt`), the audit-column interceptor (FRS §15.1) sets
   `UpdatedAt = now`. That **resets the 48h clock the job measures**. The claim also gets a new `RowVer`, so a handler editing it at the
   same moment gets a spurious 409.
2. `Claims.UpdatedAt` is nullable (FRS §9.1). A naive `UpdatedAt < now − 48h` never matches a claim that has not been edited since
   creation, and those are exactly the stale ones.

**Options.**
- (a) Add a `SlaBreached` status. This contradicts FRS §12.2 and breaks the §4.2 state machine.
- (b) Add `IsSlaBreached` / `LastSlaBreachAt` columns on Claims (the original suggestion). This runs into trap 1, unless the interceptor
  skips system writes (special-casing that is hard to defend).
- (c) Write no claim columns. Derive "last breach" from `ClaimAuditLog` (EventType = SLA_BREACH_DETECTED), and expose `isSlaBreached` as a
  computed read-model field (a breach event exists with CreatedAt > COALESCE(UpdatedAt, CreatedAt)).
- (d) Use a separate `ClaimSlaStatus` table (ClaimId, LastBreachAt). This avoids trap 1 but adds a table.

**Recommendation.** (c). The job query is `Status IN ('Draft','Open') AND COALESCE(UpdatedAt, CreatedAt) < now − 48h` and there is no
SLA_BREACH_DETECTED for the claim in the last 24h. It is backed by index `IX_ClaimAuditLog_ClaimId_EventType_CreatedAt`.

**Rationale.** It follows FRS §12.2 literally ("do not change claim status"; "track last breach event per claim"). The audit log is
"the single source of truth for all business actions" (FRS §14). No write to Claims means no clock reset and no concurrency conflict.
The brief's wording ("SlaBreached status") is covered by the computed `isSlaBreached` flag in the DTOs and a UI badge.
**Status:** ACCEPTED (2026-09-27)

## D-02 — Rule-ID numbering clash
**Context.** The same IDs mean different things in the two documents:

| ID | FRS (§7) | Brief (§3.4) |
|---|---|---|
| BR-C-02 | Out-of-period Warning; "must be cleared or acknowledged before transitioning to Open" | Warning; "the warning is recorded in the audit log" (no blocking) |
| BR-C-03 | ≥1 Claimant **before Draft → Open** | "must have at least one ClaimParty of type Claimant" (unqualified) |
| BR-C-05 | code must exist and be active | "…active in the reference table **for the organization**" |
| BR-C-06 | No-policy Warning; reserves blocked | Draft cannot go directly to Closed; required path |
| BR-C-07 | Description ≥ 20 chars | — (none) |
| BR-R-01 | amount > 0 **except SubrogationRecoverable** | amount > 0 (no exception) |
| BR-R-02 | the three thresholds | ≤ $10k auto-approved |
| BR-R-03 | **no self-approval** | $10k–$100k supervisor |
| BR-R-04 | rejected → resubmit as a new record | > $100k manager |
| BR-R-05 | $10M aggregate + manager override | GL idempotency key |
| BR-R-06 | GL idempotency key | rejected → resubmit |
| BR-R-07 | — | $10M aggregate + override |

The brief has no self-approval rule at all. It appears only in FRS §6.3, BR-R-03 and §8.

**Options.** (a) Use FRS IDs as canonical and give brief-only rules a `B-` prefix. (b) Use brief IDs as canonical. (c) Renumber everything.

**Recommendation.** (a). The only brief-only rule is **B-C-06** (Draft ↛ Closed / path; see D-20). The mapping of the other brief IDs to
FRS IDs is kept in `REQUIREMENTS-MATRIX.md`. Tests are named after the FRS IDs.

**Rationale.** CLAUDE.md names the FRS as the detailed source of truth, and brief Appendix A says "consult the full FRS documents". The
semantic differences (BR-C-02 blocking, BR-C-03 timing, BR-R-01 exception) are resolved in D-19, D-06 and D-05.
Requirements that have no FRS/brief ID get non-BR prefixes (TR-, VAL-, RSV-, PTY-, API-, JOB-, …), so the `BR-` namespace stays exactly the FRS's.

The FRS contradicts itself on message text. BR-C-06 (§7.1) says "No policy linked — claim requires policy association before financial actions
are permitted." The §8 table says "No policy linked. Policy must be associated before reserves can be set." We use the §8 wording, because
§8 is the consolidated message table.

The FRS also has two dangling cross-references, both meaning the §4.3 closure conditions CC-01..04:
- §4.2 (Open → Closed) says "see BR-CL-01", but no BR-CL-* rule is defined anywhere.
- §4.1 (Closed) says "see Section 7.6", but §7.6 is Audit Rules.

We map both to CC-01..04.
**Status:** ACCEPTED (2026-09-27)

## D-03 — Reserve component names
**Context.** Brief Appendix A.2 has IndemnityReserve / ExpenseReserve "(ALAE)" / RecoveryReserve "(negative)" / LitigationReserve. FRS §6.2
and §9.5 have Indemnity / Expense / ALAE / SubrogationRecoverable. The brief is internally inconsistent: it calls Expense "ALAE" and then
also lists a separate Litigation line, which is what FRS §6.2 calls ALAE ("attorney and court costs"). Brief §3.2 calls a component a
"reserve line per coverage type", but coverage evaluation is out of scope (FRS §2).

**Options.** (a) FRS names. (b) Brief names. (c) Brief names with FRS aliases.

**Recommendation.** (a) `Indemnity | Expense | ALAE | SubrogationRecoverable`, stored as NVARCHAR(50).

**Rationale.** These names are used consistently across FRS §6.2, §8, §9.5 and §11.2/§11.3 (the UI dropdown lists exactly them). Mapping:
Litigation → ALAE, Recovery → SubrogationRecoverable.
**Status:** ACCEPTED (2026-09-27)

## D-04 — Reserve adjust API
**Context.** FRS §10.2: `POST /api/claims/{id}/reserves` body `{component, amount, changeReason, transactionType}` for "open or adjust", with
`/approve`, `/reject` and `/retract` addressed by `{txnId}`. Brief §3.3.3: `POST /reserves` to open, `PUT /reserves/{reserveId}` to "adjust
reserve amount", and approve/reject by `{reserveId}`. The brief's §3.2 puts `ApprovalStatus` on the *component*, which implies
component-level approval (see D-21).

**Options.**
- (a) FRS contract only.
- (b) Brief contract only.
- (c) The FRS contract as primary, plus brief `PUT /reserves/{componentId}` as a thin alias that produces an Adjust transaction.

**Recommendation.** (c).
- `PUT /api/claims/{id}/reserves/{componentId}` takes body `{ newAmount, changeReason }`. The handler computes
  `delta = newAmount − CurrentAmount` and sends the **same** `SubmitReserveTransactionCommand` with `TransactionType = Adjust`. A zero delta → 422.
- Approve/reject/retract take `{txnId}` (a ReserveHistoryId). This matches the brief's routes, with `{reserveId}` read as the transaction id.

**Rationale.** Both documents' endpoint lists are satisfied with one code path. PUT with an absolute target amount is the natural REST
meaning of "adjust amount" and matches the brief's `PreviousAmount/NewAmount` (§3.2). Approval must target a transaction, because a
component can have several transactions over time (FRS §6.4 steps 9–10, §9.6).
**Status:** ACCEPTED (2026-09-27)

## D-05 — Negative amounts, transaction types, authority sign
**Context.**
- FRS BR-R-01 and §8 say "amount must be greater than zero (except SubrogationRecoverable which may be negative)".
- FRS §9.6 says `Amount` is a "Delta amount (positive = increase, negative = decrease)", and `TransactionType` is Add / Adjust / Reverse.
- FRS §11.3 colours amounts "green for increase, red for decrease".
- FRS §6.2 "May Go Negative?" is No for Indemnity/Expense/ALAE, which is a statement about the **balance**.
- FRS §4.3 CC-04 expects reserves may be open (> 0) at closure. Bringing them to zero requires decreases.

Taken literally, BR-R-01 makes it impossible to ever reduce an Indemnity reserve. Brief BR-R-01 has no Subrogation exception at all.

For authority, FRS §6.3 says "the threshold applies to the amount of the individual transaction". For a negative delta, the sign is
NOT SPECIFIED.

**Options.**
- (a) Literal: every transaction amount > 0 (except Subrogation), with no decreases. This contradicts §9.6 and §11.3.
- (b) BR-R-01 applies to *opening* (Add) a component. Adjust carries a signed non-zero delta, bounded by the §6.2 balance rule. Reverse
  zeroes the component.
- (c) Every transaction is an absolute "new balance" and the delta is derived. This loses §9.6's delta semantics.

**Recommendation.** (b):

| Type | Amount rule | Resulting balance rule |
|---|---|---|
| Add (first txn on a component; component is created) | > 0; for SubrogationRecoverable ≠ 0 | — |
| Adjust (existing component) | ≠ 0, signed | ≥ 0 for Indemnity/Expense/ALAE; no constraint for Subrogation |
| Reverse (existing component) | server-computed `−CurrentAmount`; request amount must be omitted | = 0 |

- `transactionType` is optional in the request and inferred: Add if the claim has no component of that type, else Adjust. An explicit Add on an
  existing component → 422.
- **Authority tier uses `|amount|`** of the transaction (CLAUDE.md already fixes this). Rationale: it is conservative, it is symmetric, and a large
  release of reserves is as material to the financials as a large increase.
- The §8 message "Reserve amount must be greater than zero." is kept for the Add case. The Adjust zero-delta and negative-balance cases get new
  messages (ASSUMPTION): "Adjustment amount must not be zero." / "Reserve balance for {component} cannot go below zero."

**Rationale.** This is the only reading that satisfies §6.2, §9.6, §11.3 and CC-04 at the same time, while keeping BR-R-01's intent (no
non-positive opening reserves).
**Status:** ACCEPTED (2026-09-27)

## D-06 — Intake validation: 422 vs persisted issues
**Context.**
- FRS §5.4: Critical issues mean the "claim stays in Draft". Its examples are a future loss date, an invalid cause code and no claimant. This
  implies the claim *is created*.
- FRS §8: the rules are FluentValidation validators on commands.
- FRS §10.4: the example 422 body contains **both** `LossDate` and `ClaimParties` errors.
- FRS §9.2: `LossEvents.CauseOfLossCode` is a NOT NULL FK, so an unknown code cannot be persisted.
- BR-C-03, BR-P-01 and §5.2 Step 2: a claimant is required "before Open", which implies a claimant-less Draft is legitimate.
- Brief BR-C-03 is unqualified, and FRS §11.2 / brief §3.7.2 make the claimant mandatory in the UI's Step 2.

**Options.**
- (a) Strict: every Critical → 422 on POST. Simple. Draft→Open checks become mostly redundant.
- (b) Hybrid: *structural* criticals → 422; *completeness* criticals and all warnings → persisted as `ClaimValidationIssues` on a Draft.
- (c) Persist everything as issues. This requires a nullable cause code, which contradicts §9.2.

**Recommendation.** (b):

| Rule | Severity (FRS §8) | On POST /claims |
|---|---|---|
| LossDate required | Critical | **422** |
| BR-C-01 LossDate in the future | Critical | **422** |
| BR-C-07 description < 20 chars | Critical | **422** |
| BR-C-05 unknown/inactive cause code | Critical | **422** (unpersistable: NOT NULL FK) |
| Enum shapes (party role/type, asset type, component) | Critical | **422** |
| Initial reserve rules (BR-R-01, component) | Critical | **422** |
| Initial reserve supplied while PolicyId is null (BR-C-06) | — | **422** (reserves are blocked without a policy) |
| BR-C-03 no active Claimant | Critical | persisted issue; claim is created in Draft |
| BR-C-02 loss date outside policy period | Warning | persisted issue (blocks Open until acknowledged; see D-19) |
| BR-C-06 no policy linked | Warning | persisted issue |
| No risk objects (§5.4) | Warning | persisted issue |

The 201 response includes the `validationIssues` array. The FE's pre-submit confirmation dialog (FRS §11.2 "confirmation dialog if any
Warning validation issues exist") computes the three warnings client-side. It already has the data, because the in-force badge (§11.2)
needs the same comparison.

**Rationale.** Rules that make the data meaningless or unpersistable are rejected. Rules about *completeness*, which can be fixed later by
adding a party or linking a policy, follow §5.4's "claim stays in Draft". **Caveat:** the §10.4 example and brief BR-C-03 support rejecting
a claimant-less POST, so option (a) is also defensible. The UI blocks a claimant-less submit either way, so this choice only affects direct API
callers.
**Status:** ACCEPTED (2026-09-27)

## D-07 — ClaimValidationIssues entity
**Context.** Several places imply a persisted issue list that is not in the FRS §9 entity list:
- the `VALIDATION_ISSUE_ADDED` audit event (§14.1);
- CC-02 "no unresolved Critical validation issues recorded against the claim";
- §4.2 "(or all waived)";
- BR-C-02 "cleared or acknowledged".

**Design.**

| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER | PK |
| OrganisationId | UNIQUEIDENTIFIER | tenant |
| ClaimId | FK Claims | |
| RuleCode | NVARCHAR(50) | e.g. `BR-C-02`, `BR-C-03`, `BR-C-06`, `NO-RISK-OBJECT` |
| Severity | NVARCHAR(50) | `Critical` / `Warning` (enum `IssueSeverity`) |
| Field | NVARCHAR(100) | e.g. `LossDate`, `ClaimParties` |
| Message | NVARCHAR(500) | FRS §8 wording |
| Status | NVARCHAR(50) | `Open` / `Resolved` / `Acknowledged` |
| RaisedAt | DATETIMEOFFSET(7) | |
| ResolvedAt, ResolvedByUserId, ResolutionNote | nullable | |
| + audit and soft-delete columns | | FRS §15.1 |

- Unique filtered index `(ClaimId, RuleCode) WHERE Status = 'Open' AND IsDeleted = 0`. This prevents duplicate open issues on re-validation.
- **Resolved:** the system sets this when re-evaluation finds the rule passing (claimant added → BR-C-03; policy linked → BR-C-06, which then
  re-evaluates BR-C-02). Re-evaluation runs inside the command that changes the relevant data, and also on `POST /claims/{id}/validate`.
- **Acknowledged:** Warnings only, by any role with a note (ASSUMPTION: FRS §4.2 names the "handler" as the actor for Draft → Open). Audit event:
  VALIDATION_ISSUE_ACKNOWLEDGED (an addition to §14.1).
- **Waived (Criticals):** under D-06 the only persisted Critical is BR-C-03 (no claimant). BR-ST-02(b) and §4.2 require a claimant as an
  *independent* condition, so waiving that issue could never unblock Draft → Open. **Recommendation: do not build waive**, and document this
  reasoning. Alternative, if you want it visible: supervisor+ may waive with a mandatory note.

**Rationale.** This gives CC-02 and BR-ST-02 something concrete to query and gives VALIDATION_ISSUE_ADDED a trigger. Building no dead waive path is better
than building one that can never be used.
**Status:** ACCEPTED (2026-09-27)

## D-08 — Endpoints implied but not listed
**Context.** The UI (FRS §11) and the rules require operations that FRS §10 and brief §3.3 do not list.

**Recommendation.** Add the following (all under `/api`, kebab-case per FRS §15.3; each is a Command or Query):

| Endpoint | Command/Query | Driven by | Role |
|---|---|---|---|
| `POST /claims/{id}/validate` | `ValidateClaimCommand` (persists issue changes, so it is a command) | FRS §5.4 "validate endpoint" | any |
| `PUT /claims/{id}/policy` `{policyId}` | `LinkPolicyCommand` | BR-C-06 "until a policy is linked" | any; not on Closed/Withdrawn |
| `PUT /claims/{id}/assignee` `{userId}` | `AssignClaimHandlerCommand` | §4.1 "handler set" | supervisor+ (D-18) |
| `PATCH /claims/{id}` `{notes?, severity?}` | `UpdateClaimDetailsCommand` | §11.3 Tab 1 "Claim notes field (editable)"; §9.1 Severity | any |
| `PUT /claims/{id}/reserve-limit-override` `{enabled, reason}` | `SetReserveLimitOverrideCommand` | BR-R-05, §3 manager | manager |
| `POST /claims/{id}/reserves/{txnId}/retry-posting` | `RetryGlPostingCommand` | §11.3 "retry button for Failed" | any (the reserve is already approved; retrying is idempotent) |
| `POST /claims/{id}/validation-issues/{issueId}/acknowledge` `{note}` | `AcknowledgeValidationIssueCommand` | BR-C-02 | any |
| `GET /claims/{id}/validation-issues` | `ListValidationIssuesQuery` | UI | any |
| `GET /policies/{id}/coverage` | `GetPolicyCoverageQuery` | Brief §3.3.2 | any |
| `GET /claims/{id}/documents/{docId}/url` | `GetDocumentDownloadUrlQuery` | §11.3 "Download button: calls API to get SAS URL" (a list SAS may have expired) | any |
| `POST /auth/dev-token` `{username}` | handled by the auth service (see below) | D-16 | anonymous, gated by config |
| `GET /auth/users` | `ListDemoUsersQuery` | §11.4 role switcher | anonymous, gated by config |
| `GET /users?role=handler` | `ListUsersQuery` | §11.1 handler filter; the assign dialog | any |

The auth token endpoint is infrastructure (token issuance), not a business command. It is a thin controller over `ITokenService` and does
not go through MediatR (ASSUMPTION; this is defensible because it changes no domain state).

**Rationale.** Every UI affordance in FRS §11.3 needs a backing endpoint. All the extra audit event types these endpoints produce
(POLICY_LINKED, HANDLER_ASSIGNED, CLAIM_UPDATED, RESERVE_LIMIT_OVERRIDE_SET, GL_POSTING_RETRIED, VALIDATION_ISSUE_ACKNOWLEDGED,
VALIDATION_ISSUE_RESOLVED) are additions. §14.1 lists what *must* be logged, and BR-A-02 says "every significant business action".
**Status:** ACCEPTED (2026-09-27)

## D-09 — Who may perform each status transition
**Context.** FRS §3 says supervisor capabilities include "manage claim status transitions". FRS §4.1/§4.2 name the **handler** as the actor
for Open → UnderInvestigation, UI → Open, → PendingPayment and → Withdrawn. BR-ST-04 requires the Supervisor role for reopen. Roles are
hierarchical ("All handler capabilities +", §3). Brief §3.2 gives `ClaimStatusTransitions(FromStatus, ToStatus, RequiredPermission)`.

Related point: §3 says the handler "open[s] reserves within authority (≤ $10,000)". Read with §6.3 (any role may submit, and the amount
decides the approval tier), this means authority to *auto-approve*, not a cap on submission. The PROMPTS.md Phase 7 smoke test ("add a
reserve over 10k as a handler → approve as a supervisor") confirms this reading.

**Recommendation.** Seed `ClaimStatusTransitions(FromStatus, ToStatus, MinimumRole, RequiresReason, IsSystemOnly)`, where `MinimumRole` is
compared hierarchically (handler < supervisor < manager):

| From → To | MinimumRole | RequiresReason | Extra conditions |
|---|---|---|---|
| Draft → Open | handler | no | BR-ST-02, D-18, D-19 |
| Open → UnderInvestigation | handler | no | — |
| Open → PendingPayment | handler | no | ≥1 approved reserve txn (D-21) |
| Open → Closed | handler | yes (closure reason) | CC-01..04 |
| Open → Withdrawn | handler | yes | D-26 |
| UnderInvestigation → Open | handler | no | — |
| UnderInvestigation → PendingPayment | handler | no | ≥1 approved reserve txn |
| UnderInvestigation → Closed | handler | yes | CC-01..04 |
| UnderInvestigation → Withdrawn | handler | yes | D-26 |
| PendingPayment → Closed | handler | yes | CC-01..04 |
| Closed → Reopened | **supervisor** | yes | BR-ST-04 |
| Reopened → Open | — | no | `IsSystemOnly = 1`; applied automatically in the same command |

**Rationale.** It follows the explicit per-transition text of §4.2 and BR-ST-04, reads the §3 table as additive, and makes the rule data-driven,
so `GET /api/reference/claim-statuses` (§10.3) serves the same rows the domain enforces.
**Status:** ACCEPTED (2026-09-27)

## D-10 — Gap-free claim number
**Context.** FRS §5.3 says "No gaps are permitted; no reuse even if a claim is soft-deleted", and requires an "atomic database-level counter
increment (UPDATE … OUTPUT INSERTED pattern or equivalent)". It adds: "A database SEQUENCE object or a dedicated sequence table with
optimistic concurrency is both acceptable." Brief BR-C-04 says "database sequence or equivalent". The format is `CLM-{YYYY}-{0000000}`.

**Options.**
- (a) A SQL `SEQUENCE`. It is atomic, but a value is consumed even if the transaction rolls back (e.g. a failed validation after the number
  was drawn, or a deadlock), and `CACHE` loses values on restart. **Both create gaps**, which violates "No gaps".
- (b) A counter table with optimistic concurrency (read, then write with a rowversion, then retry). This is gap-free but needs retry loops under contention.
- (c) A counter table `ClaimNumberCounters(OrganisationId, Year, LastValue)` with
  `UPDATE … SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue WHERE OrganisationId = @org AND Year = @year`, executed **inside the
  claim-creation transaction**.

**Recommendation.** (c).
- On rollback the increment rolls back too, so there are no gaps. The row's update lock serialises concurrent creates for the same org and year until commit, so
  there are no duplicates.
- The first claim of a year handles 0 rows affected by inserting `(org, year, 1)`. If the insert races with another first-of-year insert, the PK
  violation is caught and the UPDATE is retried once.
- The year comes from `TimeProvider.GetUtcNow().Year` at creation. **ASSUMPTION:** the sequence resets each year. The format embeds `{YYYY}`,
  but whether the counter resets is NOT SPECIFIED.
- Exceeding 9,999,999 in a year → a domain exception. ASSUMPTION: this is unreachable in practice.
- A unique index `(OrganisationId, ClaimNumber)` is the backstop.

**Rationale.** (c) is the only option that is both gap-free and duplicate-free under concurrency without retry logic. The cost is serialised claim
creation per organisation, held for the length of one short transaction. That is acceptable for FNOL volumes, and it is the explicit trade-off
for "no gaps". Tested with 50 parallel creates (PROMPTS.md Phase 2).
**Status:** ACCEPTED (2026-09-27)

## D-11 — CurrentAmount and the $10M aggregate
**Context.** FRS §9.5 says CurrentAmount is "sum of all posted/approved history transactions". FRS §6.6 says "sum of all posted/approved
transactions". §6.3 creates auto-approved rows as "Approved", while §9.6 has a distinct `AutoApproved` value. BR-R-05: "Total approved
reserves across all components … must not exceed $10,000,000. If a new reserve would cause the total to exceed this limit, the system generates a
Warning and requires a Manager to set an override flag on the claim before the reserve can be approved." §8 shows it as a Warning. Open questions:
- Does "posted/approved" mean Posted AND Approved, or either one?
- Do pending transactions count toward the limit?
- Does SubrogationRecoverable (negative) reduce the total?
- What happens to a ≤ $10k transaction that crosses the limit?

**Options for CurrentAmount.** (a) Σ approved (Approved + AutoApproved), whatever the posting status. (b) Σ posted only.

**Recommendation.**
- **CurrentAmount = Σ Amount where ApprovalStatus ∈ {Approved, AutoApproved}**, regardless of PostingStatus. It is recalculated in the same
  transaction as the approval.
- **PendingAmount** = Σ PendingApproval amounts, a read-model field for the §11.3 summary cards.
- **Aggregate total** = Σ CurrentAmount over Indemnity, Expense and ALAE (**excluding SubrogationRecoverable**, ASSUMPTION). Pending transactions do
  not count, because they are not approved.
- The check runs **at submission**, giving a Warning in the 201 response, and again **at approval**, where it blocks.
- A transaction whose approval would make the total exceed $10M while `Claim.ReserveLimitOverride = false` is created as **PendingApproval regardless
  of its amount**. Its required tier is **escalated to Manager**, and approval is blocked until a manager sets the override (ASSUMPTION).

**Rationale.**
- The GL posting is a downstream accounting simulation (§6.5). A GL failure must not change the claim's reserve.
- Excluding the recovery estimate is the conservative reading: an expected recovery should not create headroom for more indemnity. The literal
  reading ("all components") would be the net sum. **Vlad to confirm.**
- Escalating to Manager tier keeps one approval path: only a manager can grant the override (§3), so only a manager should approve the crossing transaction.
**Status:** ACCEPTED (2026-09-27)

## D-12 — Tenant column
**Context.** FRS §9.1 and §15.1 use `OrganisationId`. Brief §3.2 and Appendix A.4 use `OrganizationEntityId`. FRS §15.1 says "on every business table".
Brief BR-C-05 says codes must be active "for the organization", which implies tenant-scoped reference data.

**Recommendation.** Use `OrganisationId` everywhere. It goes on every business table, including child tables (LossEvents, ClaimParties, …),
Policies, CauseOfLossCodes and Users, each with a global query filter. `ClaimStatusTransitions` is global system configuration (no tenant).
There is one seeded organisation (FRS §15.1 "single fixed OrganisationId GUID"). Its id lives in the seed migration and in configuration, never in
application logic (FRS §15.4).

**Rationale.** It matches the FRS, CLAUDE.md, and the rest of the detailed spec. Filtering child tables redundantly is required by the letter of
§15.1 and protects against queries that start from a child.
**Status:** ACCEPTED (2026-09-27)

## D-13 — ClaimType / CauseOfLossCode / LossDate on Claims
**Context.** Brief §3.2 lists key fields for Claims: `ClaimNumber, PolicyId, LossDate, Status, ClaimType, CauseOfLossCode`. FRS §9.1 has none of
LossDate, ClaimType or CauseOfLossCode on Claims. FRS §9.2 puts LossDate and CauseOfLossCode on LossEvents. `ClaimType` is defined nowhere, and its
values are NOT SPECIFIED.

**Options.** (a) Denormalise LossDate and CauseOfLossCode onto Claims. (b) Keep them only on LossEvents (1:1) and join. (c) Add ClaimType as a new enum.

**Recommendation.** (b). LossEvent is 1:1 with Claim (FRS §1.2 "One loss event may give rise to one claim"), enforced by a unique index on
`LossEvents.ClaimId`. `claimType` appears in DTOs as `CauseOfLossCode.PerilCategory` (Property/Auto/Liability/…), which is a derived, read-only
value.

**Rationale.** A single source of truth avoids two writes that must be kept in sync. The list query is a 1:1 indexed join. Inventing ClaimType values
would be making up a requirement.
**Status:** ACCEPTED (2026-09-27)

## D-14 — Audit log vs soft-delete/audit-column conventions
**Context.** FRS §15.1 applies soft-delete "to all master and transaction tables" and audit columns "on all tables". FRS §9.8, BR-A-01 and §14.2
say the audit log is append-only with "no UPDATE or DELETE". An `IsDeleted` flag, or `UpdatedAt`/`UserModified`, on an immutable table is
contradictory.

**Recommendation.**
- `ClaimAuditLog` has **no** IsDeleted, DeletedAt, UpdatedAt or UserModified.
- It keeps the FRS §9.8 columns (CreatedAt, CreatedByUserId, CorrelationId, …) plus OrganisationId with a tenant filter.
- Enforcement is layered:
  1. All writes go through `IAuditLogService` (§14.2).
  2. A SaveChanges interceptor throws if any `ClaimAuditLog` entry is Modified or Deleted.
  3. The migration adds an `INSTEAD OF UPDATE, DELETE` trigger that raises an error. This works even if the app connects as dbo, which a `DENY` does not.
  4. The migration also adds `DENY UPDATE, DELETE` for a dedicated app role, when the connection user is not dbo (Azure).
- Other non-business tables follow the same exception: `ClaimNumberCounters`, `IdempotencyRecords`, and the Hangfire schema get no soft-delete.

**Rationale.** BR-A-01 is the more specific rule, and soft-deleting an audit row would be a covert delete. The trigger is defence in depth and a
good live-review talking point. **DEVIATION** from §15.1, documented here.
**Status:** ACCEPTED (2026-09-27)

## D-15 — After-commit enqueue vs outbox
**Context.** CLAUDE.md rule 5: audit writes happen before commit, and the Hangfire enqueue happens after commit. The failure window: the commit
succeeds, then the process dies before `BackgroundJob.Enqueue`. The approved transaction then sits with `PostingStatus = Pending` forever.

**Options.**
- (a) After-commit enqueue only. It is simple, and it has the gap above.
- (b) Enlist Hangfire's SQL storage in the same transaction (TransactionScope). This needs two connections in one scope, which promotes to a
  distributed transaction, and that is not available for .NET on Linux containers. Rejected.
- (c) A full transactional outbox table plus a dispatcher. This is robust, but it adds a table, a dispatcher, and a second idempotency layer.
- (d) After-commit enqueue **plus a recurring `GlPostingSweeperJob`** that treats ReserveHistory itself as the outbox.

**Recommendation.** (d). The sweeper runs every 5 minutes. It enqueues `PostGLReserveChangeJob` for rows with ApprovalStatus ∈ {Approved, AutoApproved},
PostingStatus = Pending, and ApprovedAt < now − 5 min. A duplicate enqueue is harmless, because the job is idempotent (D-23 and JOB-02).

**Rationale.** The row that needs posting already records that fact (`PostingStatus = Pending`), so a separate outbox would duplicate state. This gives
at-least-once delivery with exactly-once effect, using one extra recurring job. It is easy to explain in the live review.
**Status:** ACCEPTED (2026-09-27)

## D-16 — Mock authentication and seeded users
**Context.**
- FRS §3: "a mock authentication service is acceptable".
- FRS §10: "Authentication is Bearer token (JWT)".
- FRS §11.4: "hardcoded users", "Role switcher for testing".
- Brief §3.7.4: "a hardcoded mock auth service is acceptable — the point is that the HTTP interceptor adds a Bearer token".
- Brief §4.3 requires test credentials for at least two accounts (handler and supervisor).

Demonstrating self-approval and cross-approval needs more than one user per role. **With a single manager, a manager-submitted transaction over
$100k can never be approved**: self-approval is forbidden, and only the Manager tier qualifies.

**Options.**
- (a) Username/password login against seeded users.
- (b) `POST /api/auth/dev-token {username}` issuing a signed JWT for a seeded user, gated by configuration.
- (c) A frontend-only fake token, with the backend not validating it. This fails FRS §3's "backend: must validate caller's role". Rejected.

**Recommendation.** (b).
- Tokens are HS256 JWTs signed with `Auth:SigningKey` (Key Vault in Azure). Claims: `sub` (user id), `name`, `role`, `org`. Lifetime 8h.
- The endpoint is enabled by `Auth:DevTokensEnabled`. It is **on in the demo deployment** so reviewers can switch roles, and this is documented
  as a deliberate non-production shortcut. The API validates issuer, audience, lifetime and signature on every request.
- Seeded `Users` (via HasData; ids from the seed, never in logic):

| Username | Role |
|---|---|
| handler.alex | handler |
| handler.blake | handler |
| supervisor.casey | supervisor |
| supervisor.drew | supervisor |
| manager.emery | manager |
| manager.finley | manager |

- The "test credentials" in the README are these usernames.

**Rationale.** A real JWT pipeline on the backend meets FRS §10 and §3 enforcement, with no password handling to defend. Two users per role covers
every approval scenario in FRS §6.3–6.4, including a manager's own transaction over $100k.
**Status:** ACCEPTED (2026-09-27)

## D-17 — Angular lazy loading
**Context.** FRS §11.4 asks for "lazy-loaded feature modules (ClaimsList, ClaimDetail, FnolIntake)", and brief §3.7.4 asks for "lazy-loaded feature
modules". Angular 18 defaults to standalone components, and NgModules are optional.

**Recommendation.** Use standalone components, with one lazily loaded route file per feature:
`{ path: 'claims', loadChildren: () => import('./features/claims-list/claims-list.routes') }` (and the same for `fnol-intake` and `claim-detail`).
Each feature folder has its own components and services, and no NgModule.

**Rationale.** This produces the same separately loaded chunk per feature that an NgModule would, and it is Angular's recommended style since v17.
The requirement's intent (feature isolation and lazy loading) is met, and the wording difference is explained in ARCHITECTURE.md. Verified in Phase 6
from the build output (one lazy chunk per feature).
**Status:** ACCEPTED (2026-09-27)

## D-18 — AssignedHandlerId
**Context.** FRS §4.1: Open's entry condition is "All critical validation passes; handler set". FRS §9.1: AssignedHandlerId is nullable. BR-ST-02 lists
only (a) no criticals and (b) a claimant. How a handler gets set is NOT SPECIFIED.

**Options.**
- (a) Auto-assign the creator at FNOL.
- (b) Leave it null until an explicit assign step.
- (c) (a) plus reassignment.

**Recommendation.** (c).
- At creation, `AssignedHandlerId = current user`. Every role has handler capabilities (§3).
- `PUT /claims/{id}/assignee` requires supervisor+. The target must be an active user of the same organisation with role ≥ handler.
- Draft → Open also checks that `AssignedHandlerId` is not null, as a blocking condition with the message "A handler must be assigned before the claim can be opened." (ASSUMPTION: the wording is mine).

**Rationale.** It honours §4.1 without adding a mandatory manual step to the demo flow.
**Status:** ACCEPTED (2026-09-27)

## D-19 — Do Warnings block Draft → Open?
**Context.** FRS §5.4 says Warnings do not block ("claim may proceed to Open"). FRS BR-C-02 says the out-of-period warning "must be cleared or
acknowledged before transitioning to Open". Brief BR-C-02 says it is only recorded in the audit log.

**Options.** (a) No warning blocks. (b) All warnings must be acknowledged. (c) Only BR-C-02 must be acknowledged.

**Recommendation.** (c). Draft → Open is blocked while an **Open** BR-C-02 issue exists. It is cleared by relinking a policy whose period covers the
loss date, or by acknowledging it. The BR-C-06 and no-risk-object warnings never block Open. BR-C-06 blocks *reserves* instead.

**Rationale.** The specific rule overrides the general table. BR-C-02 is explicit and names this exact transition.
**Status:** ACCEPTED (2026-09-27)

## D-20 — Transition table: brief path vs FRS table
**Context.** Brief B-C-06: "Draft may not transition directly to Closed. Required path: Draft → Open → UnderInvestigation → PendingPayment → Closed
(or Draft → Open → Closed for trivial claims, *if configured*)". FRS §4.2 allows Open → Closed and UnderInvestigation → Closed unconditionally
(subject to CC-01..04), and it does not list Draft → Closed or Draft → Withdrawn.

**Recommendation.** Seed the FRS §4.2 table exactly (D-09). B-C-06 is satisfied, because Draft → Closed is absent. The brief's "if configured" is
realised by the table being data rather than code: removing the Open → Closed row changes the behaviour with no code change.
- Draft → Withdrawn is not allowed. It is NOT SPECIFIED, and we follow the table. A mistaken Draft simply stays in Draft.
- The "liability determined" condition on UnderInvestigation → PendingPayment (§4.2) has no backing data field (NOT SPECIFIED), so there is no extra check beyond "≥1 approved reserve".

**Rationale.** The FRS table is more detailed and explicitly exhaustive ("Any transition not listed is invalid", §4.2).
**Status:** ACCEPTED (2026-09-27)

## D-21 — Approval status: component vs transaction
**Context.** Several places treat approval as a component property:
- FRS §4.2 "At least one reserve component with status Approved";
- CC-01 "No reserve components with status PendingApproval";
- brief §3.2 ClaimReserveComponents "ApprovalStatus".

But FRS §9.5 gives the component `Status Active/Closed` only, and §9.6 puts `ApprovalStatus` on each transaction. There is also a naming conflict:
§6.3 says an auto-approved reserve is "created with status Approved", while §9.6 has a separate `AutoApproved` value.

**Recommendation.** Approval lives on **ReserveHistory**. The component-level predicates are derived:
- *component has PendingApproval* ⇔ ∃ txn with ApprovalStatus = PendingApproval (CC-01);
- *component with status Approved* ⇔ ∃ txn with ApprovalStatus ∈ {Approved, AutoApproved} (§4.2 → PendingPayment).

Use `AutoApproved` on the row (it is more informative for audit and reporting) and treat it as equivalent to Approved everywhere. The component DTO exposes
`hasPendingApproval` and `pendingAmount`, which cover brief §3.2's "ApprovalStatus".

**Rationale.** Only transaction-level approval supports several pending/rejected/approved changes over a component's life (§6.4 steps 9–10).
**Status:** ACCEPTED (2026-09-27)

## D-22 — ReserveHistory mutability; balances on pending rows
**Context.** FRS §6.6: "you do not UPDATE a reserve record. You INSERT a new transaction record." Yet §9.6 has ApprovedByUserId/At,
RejectedByUserId/At, PostingStatus and PostingJobId on the same row, and §12.1 says "update ReserveHistory.PostingStatus = Posted". So the row
**must** be updated for workflow state.

A second problem: PreviousBalance/NewBalance are computed at submission. Suppose A (+$50k, pending) is submitted, then B (+$5k, auto-approved) takes
effect on the same component. When A is later approved, its stored balances (0 → 50k) are wrong; the real movement is 5k → 55k.

**Options for balances.**
- (a) At most **one PendingApproval per component**. A new submission on a component with a pending transaction → 422 "Component has a pending
  transaction; retract it or wait for a decision." The balances stay valid, because nothing else can take effect on that component first.
- (b) Make PreviousBalance/NewBalance nullable and stamp them when the transaction takes effect.
- (c) Recompute them at approval (an update of amount-like columns, which goes against the spirit of §6.6).

**Recommendation.**
- ReserveHistory is **"amount-immutable", not "row-immutable"**. `Amount`, `PreviousBalance`, `NewBalance`, `Component`, `ChangeSequence`,
  `TransactionType`, `SubmittedByUserId` and `IdempotencyKey` are guarded by the interceptor, which throws if they are modified. Workflow columns
  (approval and posting) may change once, through domain methods only.
- For balances, choose **(a)**, backed by a unique filtered index `(ReserveComponentId) WHERE ApprovalStatus = 'PendingApproval'`.
  **ASSUMPTION:** this restriction is not in the spec. (b) is the fallback if you prefer to allow parallel pending changes.

**Rationale.** (a) keeps the balance chain linear and every stored number true, and the database enforces it. It matches common carrier practice
(one outstanding reserve change per line). §6.4's retract-then-resubmit rule already implies serial changes.
**Status:** ACCEPTED (2026-09-27)

## D-23 — `{ReserveId}` in the GL idempotency key
**Context.** FRS §6.5, BR-R-06 and §12.1 give the key as `Reserve:{ReserveId}:Change:{ChangeSequence}`, where ChangeSequence is "monotonically
increasing per component" (§9.6). "ReserveId" is not defined.

**Recommendation.** `ReserveId` = **ReserveComponentId**. ChangeSequence is assigned at *submission* from `Component.LastChangeSequence + 1`, and
that counter is protected by the component's RowVer. Rejected and cancelled transactions still consume their sequence number. The key is stored on the
row, with a unique filtered index `WHERE IdempotencyKey IS NOT NULL`.

**Rationale.** If ReserveId were the transaction id, `:Change:{Seq}` would be redundant. The component-plus-sequence pair identifies each change naturally.
**Status:** ACCEPTED (2026-09-27)

## D-24 — HTTP Idempotency-Key header
**Context.** FRS §10: "All write operations are idempotent where an Idempotency-Key header is provided." The storage, scope, conflict behaviour and
retention are NOT SPECIFIED. CLAUDE.md rule 13: store (key, user, route, response hash/status) and replay.

**Options.** (a) A MediatR behaviour. (b) An ASP.NET Core endpoint/action filter.

**Recommendation.** (b).
- Table `IdempotencyRecords(Id, OrganisationId, UserId, Key NVARCHAR(200), Method, Route, RequestHash, StatusCode, ResponseBody NVARCHAR(MAX),
  CreatedAt, CompletedAt)` with a unique index on `(UserId, Key)`.
- Flow:
  1. Insert a placeholder row. If the insert hits a unique violation: when the stored request matches and is complete → replay the stored status and
     body; when it is still in flight → 409; when the payload hash or route differs → 422 "Idempotency-Key reuse with a different request."
  2. Execute the request.
  3. Store the response. 5xx responses are not stored, so the client may retry.
- Retention is 24h (ASSUMPTION), cleaned by a small recurring job. The header is optional; without it, requests execute normally.

**Rationale.** What gets replayed is an **HTTP response** (status code + body + Location header), which MediatR cannot see. Keeping it in the web layer
leaves Application free of HTTP concerns.
**Status:** ACCEPTED (2026-09-27)

## D-25 — 403 vs 422 for authority failures
**Context.** FRS §8 lists "Approver role must be Supervisor or Manager" and "Approver must not be the submitter" as Critical validation rules, with
messages. BR-R-03 says 422 for self-approval. CLAUDE.md rule 12 maps `Forbidden → 403`.

**Recommendation.**
- **403**: the caller's role can *never* perform the endpoint. Example: a handler calling approve/reject, blocked by the `[Authorize(Policy = "Supervisor")]`
  endpoint policy. Also the reserve-limit override for non-managers.
- **422 with the FRS §8 wording**: the rule depends on the data.
  - A supervisor approving more than $100k → "Your role does not have authority to approve this reserve amount."
  - Self-approval → "Self-approval is not permitted."
  - A non-submitter retracting → "Only the submitter may retract a pending reserve." (ASSUMPTION: the wording is mine).
  - Reopen by a handler → 403. It is a role-only rule: BR-ST-04 plus D-09 MinimumRole.

**Rationale.** 403 means "you are not allowed this action at all". 422 means "this action is not valid for this particular data". The FRS's own messages
are all data-dependent. The domain re-checks role and amount even behind the endpoint policy (defence in depth, FRS §3).
**Status:** ACCEPTED (2026-09-27)

## D-26 — Closure, withdraw and reopen: API shape and side effects
**Context.** FRS §10.1: `PUT /claims/{id}/status` with body `{targetStatus, reason?}`. CC-04 says open reserves (> 0) give a "non-critical warning"
and "The handler must explicitly confirm closure … by providing a justification note". FRS §14.1 has STATUS_CHANGED **and** CLAIM_CLOSED / CLAIM_REOPENED.
What happens to reserves or pending transactions on Withdrawn is NOT SPECIFIED.

**Recommendation.**
- **Body:** `{ targetStatus, reason?, justification? }`.
- **Closing:**
  - CC-01..03 failures → 422 listing *every* failed condition (BR-ST-03).
  - CC-04 with a component `CurrentAmount > 0` and no `justification` → 422 with error key `OpenReserves` (a distinct key, so the §11.3 pre-flight
    checklist can prompt for the justification).
  - With a justification → the claim closes. `ClosureReason = reason`. The justification is stored in the CLAIM_CLOSED NewValue. `ClosedAt = now`.
- **Withdrawn:** `reason` is required (§4.2), stored in ClosureReason, and ClosedAt is set. Withdrawal is **blocked while any PendingApproval transaction
  exists** (ASSUMPTION mirroring CC-01, so no approvable transaction is left dangling on a terminal claim).
- **Reopen:** Closed → Reopened, then Reopened → Open in the same command and transaction. **Audit: STATUS_CHANGED (Closed → Reopened), CLAIM_REOPENED,
  STATUS_CHANGED (Reopened → Open), which is 3 rows.** CLAUDE.md's "two STATUS_CHANGED entries" is correct, and CLAIM_REOPENED is in addition. ClosedAt
  is cleared (ASSUMPTION); the history of the earlier close stays in the audit log.
- **Closure audit:** STATUS_CHANGED + CLAIM_CLOSED (2 rows).
- **Closed and Withdrawn claims are read-only** for reserves, parties, risk objects, documents, policy links and notes → 422 "Claim is {status}; no changes are
  permitted." (ASSUMPTION: NOT SPECIFIED. Reopen is the documented way back, §4.1.)

**Rationale.** A single endpoint serves every transition, and the FE gets a machine-readable key for the CC-04 prompt. The audit trail matches §14.1 literally.
**Status:** ACCEPTED (2026-09-27)

## D-27 — ClaimParties: IsActive vs IsDeleted
**Context.** FRS §9.3 `IsActive` means "Soft remove from claim". FRS §15.1 adds `IsDeleted` soft-delete on all tables. DELETE `/parties/{partyId}` "sets
IsActive = false" (§10.1), and Tab 2 shows "active/inactive status" (§11.3).

**Recommendation.** `IsActive = false` is the **business** removal: the party stays visible as inactive, and PARTY_REMOVED is written. `IsDeleted` is the
**technical** convention column, never set by business flows. The global query filter excludes only `IsDeleted`. Claimant counts (BR-C-03, CC-03 and the
last-claimant rule) count only `IsActive` Claimants. Reactivating a party is NOT SPECIFIED and not built.

**Rationale.** Both columns are required by different sections. They mean different things, so neither should be dropped.
**Status:** ACCEPTED (2026-09-27)

## D-28 — Documents: blob naming, local download, size limit, config key
**Context.**
- BR-D-01: path `claim-documents/{organisationId}/{claimId}/{filename}`, sanitised.
- FRS §13: the local fallback stores under `/uploads/{organisationId}/{claimId}/` and "Return[s] a local URL for download".
- BR-D-02 / §13: "Document bytes must NOT be proxied through the API."
- The config key appears as `StorageProvider` (FRS §13), `Provider` (brief §3.6), and `Storage:Provider` (CLAUDE.md).
- FRS §13 sets a 50 MB limit.
- DocumentType values are given as "E.g. PoliceReport, MedicalReport, Invoice, Other" (§9.7).

Uploading a second file with the same name would silently **overwrite** the first blob under the literal path.

**Recommendation.**
- **Blob name** `{organisationId}/{claimId}/{documentId}_{sanitisedFileName}` in container `claim-documents`. **DEVIATION:** the `{filename}` segment is
  prefixed with the document id, which prevents overwrites. `DocumentName` keeps the original name for display.
- **Sanitisation:** strip directory components, reject `..`, control characters and reserved names, normalise Unicode (NFC), and cap the length at 200 characters.
- **Local fallback:** files go under `{ContentRoot}/uploads/{org}/{claim}/…`. Downloads use a dev-only endpoint `GET /api/local-files/{token}`, where the token
  is an HMAC-signed, 1h-expiring token that emulates a SAS. This *does* stream through the API, which is unavoidable for a local filesystem. It is
  documented as a **DEVIATION from BR-D-02, limited to the fallback provider**. The Azure provider never proxies bytes.
- **Config** `Storage:Provider = AzureBlob | LocalFileSystem`.
- **Size:** the 50 MB limit is enforced in the validator. Kestrel `MaxRequestBodySize` must also be raised: its default is about 28.6 MB (30,000,000 bytes), which is below 50 MB.
- **MIME allowlist** (FRS §13), checked on both the declared type and the file signature (Phase 5).
- **DocumentType** enum = `PoliceReport | MedicalReport | Invoice | Other` (ASSUMPTION: the "e.g." list is taken as complete; `Photo` could be added cheaply).

**Rationale.** It is data-safe, keeps the spec's folder structure, and states the one unavoidable deviation honestly.
**Status:** ACCEPTED (2026-09-27)

## D-29 — Claims-list API vs dashboard
**Context.**
- FRS §11.1 has a "Cause of Loss" column, but the §10.1 summary row lacks it.
- The status filter is a "multi-select" in the UI but a single `status` query parameter in the API.
- The "assigned handler (text search)" filter in the UI does not match the `assignedHandlerId` (GUID) parameter.
- Page-size defaults are NOT SPECIFIED.
- The `totalReserves` definition is NOT SPECIFIED.

**Recommendation.**
- The summary DTO adds `causeOfLossCode`, `causeOfLossName`, `assignedHandlerName`, `severity` and `isSlaBreached`.
- `status` may repeat (`?status=Open&status=Draft`).
- The handler filter is an autocomplete over `GET /users?role=handler` that resolves to `assignedHandlerId`.
- `totalReserves` = net Σ CurrentAmount over all components, including negative Subrogation. It is a display figure only. The $10M check uses the gross figure from D-11.
- Paging: `page` (1-based) and `pageSize` (default 25, max 100). Response `{items, totalCount, page, pageSize}`.
- Sort: `ReportedDate desc` (ASSUMPTION).

**Rationale.** It closes every UI↔API gap with additive, backward-compatible parameters.
**Status:** ACCEPTED (2026-09-27)

## D-30 — Primary-key generation
**Context.** FRS §15.1/§15.2 call for `DEFAULT NEWSEQUENTIALID()`. If the database generates ids, they are unknown until SaveChanges. Before-commit domain
event handlers run *before* SaveChanges, and they need the ids to write audit `RelatedEntityId`, and the claim id for ClaimId.

**Options.**
- (a) Database-generated only. This needs two SaveChanges, or event handlers holding entity references and resolving ids late.
- (b) The domain assigns ids at construction, using an SQL-Server-friendly sequential GUID generator (the same algorithm EF Core uses client-side). The
  column keeps `HasDefaultValueSql("NEWSEQUENTIALID()")` for rows inserted outside EF, such as seed or raw SQL.
- (c) `Guid.CreateVersion7()`. It is time-ordered, but SQL Server sorts `uniqueidentifier` by its last 6 bytes first, so v7 values fragment the clustered index.

**Recommendation.** (b). EF sends the explicit value whenever the property is non-default, so the DB default is a harmless fallback that still meets the convention.

**Rationale.** Entities are complete (they have an identity) from birth, which is a DDD invariant. Events carry real ids, and there is a single SaveChanges per command.
**Status:** ACCEPTED (2026-09-27)

## D-31 — Tenant and soft-delete filters in background jobs
**Context.** CLAUDE.md drives the tenant filter from `ICurrentUser.OrganisationId`, but Hangfire jobs run with no HTTP user. In EF Core 9,
`IgnoreQueryFilters()` removes **all** filters on an entity. Named/selective filters arrive only in EF Core 10.

**Recommendation.** Introduce an `ITenantContext` (current OrganisationId). HTTP requests populate it from the JWT `org` claim, and jobs set it explicitly.
- The GL job receives `ClaimId`, reads the row's OrganisationId with a narrow `IgnoreQueryFilters()` lookup that re-applies `!IsDeleted` by hand, then sets
  the tenant scope and proceeds normally.
- The SLA job iterates organisations (from the `Organisations` table), setting the scope for each org.
- Every `IgnoreQueryFilters()` call sits in one repository method with a comment explaining why.

**Rationale.** It prevents two silent failures: jobs that see nothing (filter on org = empty GUID), and jobs that see deleted rows (filter bypassed).
**Status:** ACCEPTED (2026-09-27)

## D-32 — Date semantics
**Context.**
- Policy `EffectiveDate` and `ExpirationDate` are `DATE` (§9.10), while `LossDate` is `DATETIMEOFFSET(7)` (§9.2).
- FRS §14.2 and §15.1 say timestamps are stored in UTC.
- Whether policy dates are inclusive is NOT SPECIFIED.
- Clock-skew tolerance for "not in the future" is NOT SPECIFIED.
- Claims has `ReportedDate` ("When FNOL was received", §9.1) and LossEvents has `ReportDate` ("Date claim was formally reported", §9.2), both NOT NULL. The FNOL form has no field for either (§11.2).

**Recommendation.**
- LossDate is normalised to UTC on write. The BR-C-02 check compares `DateOnly(LossDate.UtcDateTime)` against `[EffectiveDate, ExpirationDate]`,
  **inclusive at both ends** (ASSUMPTION).
- BR-C-01 is `LossDate > TimeProvider.GetUtcNow()` → 422, with no tolerance (ASSUMPTION). The FE date picker blocks future dates anyway.
- `Claims.ReportedDate` = `LossEvents.ReportDate` = server UTC now at creation.
- Edge case: a loss late in the evening local time on the expiry date can fall on the next UTC date. This is documented, not solved (NOT SPECIFIED).

**Rationale.** The rule is simple, deterministic and testable with a fake `TimeProvider`.
**Status:** ACCEPTED (2026-09-27)

## D-33 — Minor gaps (one line each; all ASSUMPTION unless cited)
- **Severity** (§9.1 Catastrophic/Critical/Standard/Minor): optional at intake, default `Standard`, editable via `PATCH /claims/{id}`. Who sets it is NOT SPECIFIED.
  It is a separate enum `ClaimSeverity`, distinct from `IssueSeverity` (Critical/Warning), because the word "Critical" collides.
- **PolicyNumber/ClientName** (§9.1, denormalised): copied from the policy on create or link. They are null when there is no policy, and the UI shows "Unknown policy".
- **Policy.CoverageTypes** (§9.10 "comma-separated or JSON array"): a JSON array in NVARCHAR(MAX) via a value converter and a `ValueComparer`. It is served by
  `GET /policies/{id}/coverage` and demonstrates value conversion (brief §6.1).
- **Policy.Status** (§9.10): seeded statically. `POL-2023-000099` = Expired, the rest Active. The UI's in-force badge uses the *dates*, not the Status (§11.2).
- **System actor:** jobs write `CreatedByUserId = null`, and the UI shows "System".
- **Withdraw/reopen "reason code"** (§4.1, §4.2): free text, required and non-empty. No code list is specified.
- **Component Status Closed** (§9.5): no trigger is specified. Components stay `Active`, and the column exists for future use.
- **Risk objects:** add is built, because FNOL and the FRS UI need it. Removing risk objects is NOT SPECIFIED and is not built. `IsPrimary`: the first risk object defaults to primary.
- **Pagination of audit** (§10.1, §11.3): default 50 per page, reverse-chronological by `CreatedAt`, then `Id`.
- **Currency:** NOT SPECIFIED. Implicitly USD (FRS figures are in $). There is no currency column; the `Money` value object is the extension point (REVIEW-PREP).

**Recommendation.** Adopt each bullet as written. Each one is small and reversible, and none changes an FRS-specified behaviour.
**Rationale.** These choices fill gaps where the spec is silent. Documenting them avoids silent choices (CLAUDE.md working agreement).
**Status:** ACCEPTED (2026-09-27)

## D-34 — Seeded policies expire around the review date
**Context.** The FRS §5.5 seed dates are fixed. Today is 2026-09-27. `POL-2024-001002` (Harborview) **expired on 2026-05-31**, and `POL-2024-001001` and
`POL-2025-002002` expire on **2026-12-31**. If the live review happens in 2027, only `POL-2025-002001` would still be in force. FRS §5.5 says "you may add more".

**Recommendation.** Seed the 5 required policies exactly. Add 3 extra policies with effective 2025-01-01 and expiry 2030-12-31 (varied coverage types), so the demo
always has in-force policies. Harborview becomes a second natural demo of the BR-C-02 warning.

**Rationale.** A live demo must not depend on the calendar. The required rows are untouched.
**Status:** ACCEPTED (2026-09-27)

## D-35 — Hangfire retries and the exhausted-retries path
**Context.** FRS §12.1: "Hangfire retries automatically (default retry policy). After all retries exhausted: set PostingStatus = Failed, write GL_POSTING_FAILED."
Hangfire's default is 10 attempts with growing delays, so the Failed state appears only after many hours. That is impossible to demo.

**Options.**
- (a) Keep the default of 10.
- (b) `[AutomaticRetry(Attempts = 3)]`, with the job itself detecting the final attempt via `PerformContext` RetryCount. On the last attempt it catches the error,
  sets Failed, and writes GL_POSTING_FAILED in one transaction.
- (c) A global `IApplyStateFilter` on `FailedState`.

**Recommendation.** (b), with the attempt count in config (`Jobs:GlPosting:MaxAttempts`, default 3). **DEVIATION** from "default retry policy", made for
demonstrability. It is also explicit code that a reviewer can read and a unit test can drive. A test-only failure switch
(`Jobs:GlPosting:SimulateFailure`) makes the failure path demonstrable.

**Rationale.** A filter (c) works too, but it puts business logic in Hangfire plumbing. Option (b) keeps it next to the job.
**Status:** ACCEPTED (2026-09-27)

## D-36 — Azure hosting: Container Apps (scale to zero) + serverless SQL
**Context.** Brief §3.8 encourages the "free tier or trial". Brief §2.3 allows "App Service or Container Apps", although §3.8's minimum list names
"Azure App Service (backend)". Hangfire's SQL storage polls the database continuously while a Hangfire server is running. A serverless Azure SQL database
therefore **never auto-pauses while the API is up**, and the Azure SQL free offer (100,000 vCore-seconds/month; at the 0.5 vCore minimum that is roughly
55 awake hours) would run out within days if the API ran 24/7. App Service F1 has no "Always On", and B1 is always on, which keeps the database awake.

**Options.**
- (a) App Service B1 + Azure SQL Basic (5 DTU): about USD 18/month, always warm. This was the original recommendation.
- (b) **Azure Container Apps (Consumption plan) with `minReplicas = 0`** + Azure SQL serverless **free offer** with auto-pause. When there is no HTTP
  traffic, ACA scales the API (and its in-process Hangfire server) to zero. Nothing then holds a connection, so the database auto-pauses.
- (c) App Service F1 + serverless free offer: the app sleeps unpredictably, with a daily CPU quota. Rejected.

**Decision (Vlad, 2026-09-27): (b) Container Apps, to reduce database cost.** The deployment:
- **API:** a Container App (Consumption, 0.5 vCPU / 1 GiB) with ingress enabled, HTTP scale rule, `minReplicas = 0`, `maxReplicas = 1`. With one replica, only one Hangfire server
  runs, which keeps the demo easy to reason about. Multiple replicas would also be safe, because Hangfire distributes jobs through SQL.
  System-assigned managed identity. Secrets are Key Vault references in ACA secrets. ACA's monthly free grant covers the idle demo.
- **Image registry:** **GitHub Container Registry** (ghcr.io; free) instead of ACR Basic (~USD 5/month). The image contains no secrets, because all config comes from
  env and Key Vault, so the package can be public, which means no registry credential. ACR Basic is the fallback if you want it private.
- **DB:** Azure SQL Database **serverless, free offer**, auto-pause on, using the shortest supported auto-pause delay (verify the minimum in Phase 7). The
  free-limit behaviour is set to **"continue using the database for additional charges"**, so an exhausted allowance can never take the database offline during the
  review (cost beyond the allowance is per vCore-second and small at demo volumes).
- **Frontend:** Static Web App (Free). **Storage:** Standard LRS. **Key Vault:** Standard. **App Insights:** optional.

**Consequences (documented deviations and trade-offs).**
1. **Cold start.** The first request after idle waits for the container to start (seconds) *and* for the database to resume (up to about a minute). EF Core
   `EnableRetryOnFailure` covers the database-resuming transient errors (e.g. 40613). The UI shows its loading state. **For the live review, set
   `minReplicas = 1`** with one `az containerapp update` command beforehand, and warm the database. This goes in the README runbook.
2. **SLA job while scaled to zero (FRS §12.2 "every 15 minutes").** Recurring jobs run only while a replica is up. When the API wakes, Hangfire's recurring
   scheduler fires the missed occurrence once. The SLA rule is state-based (48h age at run time, with 24h dedupe from the audit log), so a delayed run produces the same
   entries, only detected later. **DEVIATION**, accepted for cost. An ACA scheduled Job was rejected: waking every 15 minutes would stop the database from pausing, and
   FRS §12 requires Hangfire.
3. **GL jobs survive scale-in.** Jobs are persisted in SQL. A job interrupted by scale-in is re-fetched after Hangfire's invisibility timeout when a replica returns.
   The job is idempotent (JOB-02), and the sweeper (D-15) covers the commit→enqueue gap.
4. **Brief §3.8 wording.** It lists "App Service" among the minimum resources, but brief §2.3 explicitly allows Container Apps. ARCHITECTURE.md states this.
5. **Build pipeline** gains a `docker build` + push to ghcr.io step, and `az containerapp update --image`. EF migrations still run from the pipeline as a migrations bundle.

**Rationale.** It meets the cost goal (close to USD 0 while idle) without breaking any functional rule. The only behavioural cost is SLA detection latency while nobody
is using the system, when no human would see the result anyway.
**Status:** ACCEPTED (2026-09-27) — changed by Vlad from option (a) to option (b)

## D-37 — Package versions and licences
**Context.** CLAUDE.md pins MediatR 12.x and AutoMapper 14.x (the last Apache-2.0 majors; later majors need a commercial key), and names Shouldly or
FluentAssertions 7.x (v8+ is commercial). Brief §2.3 says "MediatR 12+".

**Recommendation.** MediatR 12.x (latest patch), AutoMapper 14.x (latest patch), **Shouldly** (MIT, no version trap), xUnit, Testcontainers.MsSql,
NetArchTest.Rules. Versions are pinned in `Directory.Packages.props`. The exact versions and licences are **verified on NuGet in Phase 1** and recorded here.

**Rationale.** Pinning licence-safe majors avoids a runtime licence warning and removes a reviewer question.
**Status:** ACCEPTED (2026-09-27)

**Phase 1 verification (2026-09-27, nuget.org catalog `licenseExpression`).** Pinned in `Directory.Packages.props` with
`CentralPackageTransitivePinningEnabled`:

| Package | Version | Licence | Note |
|---|---|---|---|
| MediatR | 12.5.0 | Apache-2.0 | last 12.x; 13.0+ requires a licence key. In 12.5 `RequestHandlerDelegate` takes a `CancellationToken` |
| AutoMapper | 14.0.0 | **MIT** | last 14.x; 15.0+ requires a licence key. **Correction:** CLAUDE.md said "last Apache-licensed"; AutoMapper 14 is MIT (CLAUDE.md fixed) |
| FluentValidation (+ DependencyInjectionExtensions) | 12.1.1 | Apache-2.0 | |
| Microsoft.EntityFrameworkCore.SqlServer / .Design, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.Extensions.* | 9.0.20 | MIT | latest .NET 9 servicing |
| Microsoft.IdentityModel.JsonWebTokens | 8.19.2 | MIT | the exact version JwtBearer 9.0.20 depends on; mixing IdentityModel versions breaks at runtime |
| Serilog.AspNetCore | 9.0.0 | Apache-2.0 | |
| Swashbuckle.AspNetCore | 9.0.6 | MIT | |
| xunit 2.9.3, xunit.runner.visualstudio 3.1.5 | | Apache-2.0 | |
| Shouldly | 4.3.0 | BSD-3-Clause | |
| NetArchTest.Rules | 1.3.2 | MIT (licence file; no SPDX expression in the catalog) | |
| Testcontainers.MsSql | 4.15.0 | MIT | |
| Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4, Microsoft.Extensions.TimeProvider.Testing 9.10.0 | | MIT | |
| dotnet-ef (local tool, `dotnet-tools.json`) | 9.0.20 | MIT | |
| Hangfire 1.8.x, Azure.Storage.Blobs 12.x, Azure.Identity 1.x | — | LGPL-3.0 / MIT / MIT | added in Phases 4–5 |

**Amendment (2026-09-27): AutoMapper 14.0.0 has CVE-2026-32933 / GHSA-rvv3-g6hj-g44x (high).** Mapping a deeply self-referencing object graph
(about 25,000 levels) overflows the stack and kills the process. Affected: every version below 15.1.1 (and 16.0.0–16.1.0). **No 14.x patch exists**; the
fixed majors are commercial. With `TreatWarningsAsErrors`, the NuGet audit (NU1903) fails the build.
- Options: (a) stay on 14.0.0 and suppress **only this advisory**; (b) upgrade to 16.x with a (free Community) licence key; (c) keep 14.0.0 and downgrade
  NU1903 to a warning solution-wide.
- **Decision (Vlad, 2026-09-27): (a).** `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-rvv3-g6hj-g44x" />` in `Directory.Build.props`.
  Every other advisory still fails the build.
- **Why it is not exploitable here:** we map only DB-loaded entities to DTOs, never request input; no mapped type references itself; System.Text.Json
  rejects request bodies nested deeper than 64 levels anyway.
- **Guards (tests):** `CONV_15_Mapped_types_are_not_self_referencing` (walks every mapped source/destination type graph for cycles) and
  `CONV_15_Requests_are_never_mapped` (no map from a MediatR request type). If either ever fails, revisit (b).

## D-38 — Phase 1 cross-cutting choices
**Context.** Phase 1 had to decide several points that neither the FRS nor the brief specifies (NOT SPECIFIED), plus two refinements of earlier
decisions. None changes an FRS business rule. Listed here so none is a silent choice (CLAUDE.md working agreement).

**Choices (all ASSUMPTION unless cited).**
1. **Error bodies.** Every error is a ProblemDetails with a short `type` code instead of a URI (`ValidationError`, `NotFound`, `Forbidden`, `Conflict`,
   `Unauthorized`, `ServerError`, …), mirroring FRS §10.4's `"type": "ValidationError"`.
   - Every 422 has **exactly** `type`, `title`, `status`, `errors`. That covers FluentValidation failures, domain `BusinessRuleViolationException`s and
     unbindable input. Framework-generated 401/403/404 bodies are normalised the same way (`traceId` removed).
   - The correlation id travels in the `X-Correlation-Id` response header, not the body, so the 422 body stays exactly as specified.
   - 500 bodies carry no exception detail outside Development.
2. **Validation stays in the pipeline.** `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true`: model binding only binds, and FluentValidation
   in the MediatR pipeline produces the messages (CLAUDE.md rule 2). Otherwise `[ApiController]` would answer a missing field with its own wording before our
   validator runs, which matters for the verbatim FRS §8 messages.
3. **Unknown routes.** The authorization fallback policy (authenticated user) also applies when no endpoint matches, so an anonymous caller gets 401 and a
   signed-in caller gets 404. Kept as-is: anonymous callers cannot probe which routes exist.
4. **Audit and soft-delete columns are EF Core shadow properties** (CreatedAt, UpdatedAt, UserCreated, UserModified, IsDeleted, DeletedAt), applied to every
   entity by one convention (`ModelBuilderConventions.ApplyStandardColumns`) together with `NEWSEQUENTIALID()` and the soft-delete filter. The domain carries no
   persistence bookkeeping, and brief §6.1 explicitly checks "shadow properties". Read models that need them use `EF.Property<T>(…)` in Persistence.
   `ClaimAuditLog.CreatedAt` stays a real property (it is business data, FRS §9.8; D-14). Enums map to NVARCHAR(50) and decimals to (19,4) by pre-convention.
5. **Users/Organisations tables arrive in Phase 1.** D-16 seeded users are needed for the dev token. The `InitialCreate` migration therefore holds Organisations
   and Users only. Phase 2 adds the claims schema; because nothing is deployed yet, Phase 2 may regenerate `InitialCreate` as one migration.
6. **Usernames are unique system-wide** (`UX_Users_Username`), not per `(OrganisationId, Username)` as ARCHITECTURE-PLAN §3 first said. Sign-in resolves the user
   *before* the tenant is known (the tenant is what the token establishes), so the lookup is not tenant-scoped. When the tenant filter arrives (Phase 2), the
   sign-in lookup is the documented exception (D-31 pattern).
7. **Dev-token flow (refines D-08).** Reading the user is a MediatR query (`GetUserByUsernameQuery`; the role switcher uses `ListDemoUsersQuery`), per CLAUDE.md
   rule 1. Only token *signing* (`ITokenService`) sits outside MediatR. An unknown or inactive username → 401. An empty username → 422 from the validator.
8. **JWT claims.** `sub`, `name` (display name), `role` (the FRS §3 code: `handler` / `supervisor` / `manager`), `org`, plus `jti`, `iat`, `nbf`, `exp`, `iss`,
   `aud`. HS256 only (`ValidAlgorithms`), 1-minute clock skew, inbound claim mapping off. JSON bodies use enum *names* (`"Handler"`). Policies `Handler` /
   `Supervisor` / `Manager` admit that role and every role above it; the hierarchy is `UserRole.IsAtLeast` in the Domain, and D-09 MinimumRole reuses it.
   A fallback policy requires authentication everywhere except `[AllowAnonymous]` endpoints (auth, health).
9. **Signing key.** `Auth:SigningKey` (≥ 32 characters, validated at start-up). Development uses a clearly labelled local key in
   `appsettings.Development.json`; the base `appsettings.json` has none, so a non-development host refuses to start without one. In Azure it arrives as the
   Container Apps secret `Auth__SigningKey`, which references Key Vault (D-36). No Key Vault SDK is needed in code.
10. **Correlation id.** ~~It is accepted from the client only if it is 1–64 characters of `[A-Za-z0-9-_.]`~~ **Amended by D-39 Q1 (2026-09-27):** it is
    accepted from the client only if it is a GUID in the 36-character "D" form, because ClaimAuditLog.CorrelationId is a GUID (FRS §9.8); otherwise it is replaced with a new GUID, never
    rejected. It is echoed in the response, pushed into the logging scope for the whole request, and exposed to the SPA through CORS.
11. **Health.** `/health/live` has no dependencies, so a paused serverless database never restarts the container. `/health/ready` checks the database. Both are
    anonymous. These are the Container Apps probes (D-36).
12. **Logging.** Serilog. Levels come from configuration. The sink is chosen in code: a readable template in Development, compact JSON elsewhere (Log Analytics).
    `LoggingBehavior` logs request name, outcome and duration, but never payloads (they contain personal data). 4xx outcomes are logged without a stack trace;
    5xx with one.
13. **Toolchain.** Everything targets `net9.0` / C# 13. `global.json` pins SDK 9.0.100 with `rollForward: latestMajor`, so a newer SDK can build locally;
    `AnalysisLevel` is pinned to 9.0 so every SDK reports the same warnings. The authoritative build and test run uses the .NET 9 SDK (`mcr.microsoft.com/dotnet/sdk:9.0`,
    the same image as the Dockerfile and, later, CI).
14. **Container image.** One Dockerfile with a `runtime` target (non-root `app` user, port 8080) and a `migrator` target (EF Core migrations bundle). The
    compose `app` profile runs migrate → API. This is also how Phase 7 runs migrations against Azure SQL.

**Rationale.** Each item is small and reversible, and each closes a gap the phase could not leave open.
**Status:** PROPOSED (2026-09-27): awaiting Vlad's review.

## D-39 — Phase 2 domain and schema choices
**Context.** Phase 2 built the Claim aggregate and its schema. Two points need Vlad's decision (Q1, Q2); the rest are choices where the
FRS/brief are silent or where the Phase 0 plan turned out to need a refinement. None changes an FRS business rule.

**Open questions (need a decision before the migration is generated).**
- **Q1. `ClaimAuditLog.CorrelationId` type.** FRS §9.8 says GUID. D-38 item 10 (PROPOSED) accepts any client correlation id of 1–64
  `[A-Za-z0-9-_.]` characters (e.g. `fnol-intake-42`), which does not fit a UNIQUEIDENTIFIER.
  - (a) Keep UNIQUEIDENTIFIER; the middleware accepts a client id only if it is a GUID and otherwise generates one (the generated id is
    echoed, so the client can still correlate). Changes D-38 item 10 and one Phase 1 test.
  - (b) NVARCHAR(64); **DEVIATION** from FRS §9.8, no change to Phase 1.
  - (c) Keep UNIQUEIDENTIFIER and store NULL when the id is not a GUID (loses the link for those requests).
  - **Recommendation: (a)**: the FRS column type stays exact, and D-38 is still PROPOSED, so changing it is cheap.
  - **Decision (Vlad, 2026-09-27): (a).** D-38 item 10 amended; `API_CORR_Non_guid_correlation_id_is_replaced` covers `fnol-intake-42` and the braced form.
- **Q2. Loading the aggregate for commands.** ARCHITECTURE-PLAN §2.2 said a command loads "only the reserve transactions it needs". The
  domain rules (one pending per component, CC-01, "an approved reserve exists", CurrentAmount = Σ approved) are simplest and safest when the
  aggregate sees the **whole** history. **Recommendation:** load the full aggregate (split query). A claim has tens of transactions, not
  thousands; if that ever changes, the component already stores the `CurrentAmount` projection and `LastChangeSequence`, so partial loading
  can be added behind the repository without touching the domain.
  - **Decision (Vlad, 2026-09-27): approved** (full aggregate load). ARCHITECTURE-PLAN §2.2 updated.

**Choices (ASSUMPTION unless cited).**
1. **Primary key column is `Id`** in every table (FRS §9 names them `ClaimId`, `ReserveHistoryId`, …; §9 says "you do not need to copy the
   schema verbatim"). Foreign keys keep the FRS names (`ClaimId`, `ReserveComponentId`).
2. **C# `ReserveTransaction` maps to table `ReserveHistory`.** FRS §15.3 says entity names match table names; a class called
   `ReserveHistory` that represents one transaction reads wrongly in code. The table keeps the FRS name. `ReserveComponent` →
   `ClaimReserveComponents` likewise.
3. **OrganisationId** is a shadow property on every tenant table except `Users` (a real property since Phase 1). It is stamped on insert from
   `ITenantContext` by `AuditColumnsInterceptor`, which also refuses an insert for another organisation and any change of the column. FKs to
   `Organisations` exist on root and reference tables (Claims, Policies, CauseOfLossCodes, Users, ClaimNumberCounters); child tables inherit
   the tenant through their claim.
4. **LossEvents → CauseOfLossCodes** is a composite FK `(OrganisationId, CauseOfLossCode)` → alternate key `(OrganisationId, Code)`, so a
   claim can only reference a code of its own organisation (BR-C-05 backstop, D-12).
5. **Columns added beyond FRS §9:** Claims `ReserveLimitOverride`, `…Reason`, `…ByUserId`, `…At` (BR-R-05, D-11);
   ClaimReserveComponents `LastChangeSequence` (D-23); ReserveHistory `RequiredAuthority` and `ExceedsAggregateLimit` (BR-R-02, BR-R-05);
   CauseOfLossCodes `Notes` (the FRS §5.6 seed table has a Notes column that §9.9 omits).
6. **ReserveHistory:** `IdempotencyKey` is NOT NULL and uniquely indexed (not filtered), because every row gets its key at submission (D-23
   said "filtered WHERE NOT NULL"). `SubmittedByUserId` is NOT NULL (every submission has a user). `RejectionReason` is NVARCHAR(MAX) ("free
   text", FRS §15.1). An auto-approved row has `ApprovedAt` set and `ApprovedByUserId` NULL; its status says who decided.
7. **One active issue per rule:** the unique filtered index on ClaimValidationIssues covers `Status IN ('Open','Acknowledged')` (D-07 said
   Open only), so an acknowledged warning is not raised again by re-validation. Linking a different policy resolves an acknowledged BR-C-02,
   because the acknowledgement was for the old policy's period.
8. **Reject** needs the same authority as approve (FRS §3 "approve/reject reserves up to …"). **Self-rejection is not blocked**: BR-R-03
   protects against approving one's own money; retract is the documented path for the submitter.
9. **Aggregate limit:** only an increase of a cost component can cross the limit (`delta > 0`), so a reserve release is never escalated
   or blocked, even on a claim that is already over the limit.
10. **Entry conditions for Open** are checked on every requested move to Open (also UnderInvestigation → Open), because BR-ST-02 is stated for
    "transitioning to Open". They can only fail from Draft in practice. The system-only Reopened → Open step has no conditions.
11. **Draft claims accept reserve transactions**, because the FNOL initial reserve (FRS §5.2 step 3) is submitted on the Draft claim.
12. **No `Money` value object.** Amounts are `decimal` with one guard (`Amounts.HasValidScale`: at most 4 decimal places and 15 integer digits,
    what DECIMAL(19,4) stores), so SQL Server never rounds or rejects a value silently. A `Money` type mapped through value converters would
    break SQL translation of sums in the read models. The multi-currency answer in REVIEW-PREP is updated accordingly.
13. **NotFoundException and ForbiddenAccessException moved to the Domain**, so the aggregate can report an unknown child id (404) and a role
    that can never act (403, D-25) itself.
14. **UnitOfWork** (transaction inside the execution strategy, change tracker cleared per attempt) exists now; domain-event dispatch and the
    `UnitOfWorkBehavior` arrive in Phase 3 with the first command, where they can be tested end to end.
15. **ClaimAuditLog append-only in the database:** an `INSTEAD OF UPDATE, DELETE` trigger that raises an error, plus a `claims_app` role with
    `DENY UPDATE, DELETE` (used from Phase 7 when the app stops connecting as dbo). EF is told about the trigger (`HasTrigger`) so it does not
    use `OUTPUT` without `INTO` on that table.
16. **ClaimNumberCounters** has a check constraint `LastValue BETWEEN 1 AND 9999999` as a backstop to `ClaimNumber.MaxSequence`.
17. **Unreachable-by-design guards are kept and documented:** with the FRS table, CC-02/CC-03 cannot fail (a claim reaches Closed only through
    Open, which needs a claimant, and the last claimant cannot be removed); they are tested with a configured Draft → Closed row, which is
    exactly the brief's "if configured" case (D-20). D-18's "handler must be assigned" cannot fail either, because the creator is assigned.
18. **`IdempotencyRecords`** (D-24) is created in Phase 3 together with the filter that uses it, not in this migration.

**Rationale.** Each item is small and reversible, and each is visible in the schema or the tests, so none is a silent choice.
**Status:** ACCEPTED (2026-09-27): Q1 → (a), Q2 → full load; the 18 choices accepted as written, to be revisited after the first deployed version if needed.

## D-40 — Phase 3 API, read-side and pipeline choices
**Context.** Phase 3 built the commands, queries, validators and controllers for FNOL, claims, parties, status, validation issues,
policies and reference data. Two points needed Vlad's decision (Q1, Q2). The other items are choices where the FRS and the brief say
nothing, or refinements of earlier decisions. None changes an FRS business rule.

**Decisions by Vlad (2026-09-28).**
- **Q1. Adding risk objects after FNOL.** Neither FRS §10 nor D-08 has an endpoint for it, so the "no risk objects" warning (FRS §5.4) could only
  be avoided at intake. **Decision: add `POST /api/claims/{id}/risk-objects`** (`AddRiskObjectCommand`, API-30). It reuses the FNOL
  risk-object rules. The first risk object becomes primary (D-33). Audit: RISK_OBJECT_ADDED.
- **Q2. Idempotency-Key in Phase 3.** The Phase 3 prompt did not list it, but D-39 item 18 scheduled it here. **Decision: keep it in Phase 3.**

**Choices (ASSUMPTION unless cited).**
1. **Read side in Persistence, behind Application contracts.** `IClaimQueries`, `IReferenceDataQueries`, `IPolicyQueries` and `IUserQueries` are
   defined in Application and return Application DTOs. Persistence implements them in SQL. Reason: the claim rows read EF shadow columns
   (UpdatedAt, CreatedAt) and correlated subqueries, and Application may not reference EF Core (CONV-14). Claim rows use hand-written `Select`
   projections, and simple child rows use AutoMapper `ProjectTo` with the Application profiles. The query count is fixed and tested:
   the list runs 2 queries (count and page), and the detail runs 7, whatever the number of children.
2. **Domain-event dispatch.** There are two explicit handler interfaces, `IBeforeCommitHandler<T>` and `IAfterCommitHandler<T>`, rather than
   MediatR notifications, which have no notion of commit. The Unit of Work dispatches before-commit handlers, in a loop until no new events
   appear, then saves, commits, and dispatches after-commit handlers outside the replayed execution-strategy block. A failing after-commit
   handler is logged and does not stop the others or fail the request, because the commit already happened. Phase 3 has **no production
   after-commit handler**: an auto-approved initial reserve keeps `PostingStatus = Pending` until Phase 4 adds the GL enqueue and the sweeper (D-15).
3. **Lenient JSON binding**, so that validators, not the serializer, word the 422 (FRS §8):
   - an unknown enum name (or a number) binds as 0, which no domain enum defines, and IsInEnum reports it (for example "Invalid reserve component type.");
   - an empty or unparseable optional date-time binds as null, which gives "Loss date is required." (FRS §8 "Must be a valid date");
   - a date without an offset is read as UTC (D-32);
   - query-string binding errors (for example `?perilCategory=Volcano`) keep the framework wording, still in the FRS §10.4 shape.
4. **Error keys** are the request's property paths, so the UI can put each message next to its control: `Parties[0].FirstName`,
   `InitialReserve.Amount`, and top-level `FirstName` for "add party". Where FRS §8 names a field, the property has that name (LossDate,
   LossDescription, CauseOfLossCode, PolicyId). Errors about the claim's state keep the domain keys (StatusTransition, OpenReserves,
   ClaimParties, Claim, …).
5. **Initial reserve at FNOL.** It is always an Add transaction, submitted through the same domain method as Phase 4. The FNOL form has no reason
   field, so a blank reason becomes "Initial reserve at FNOL.". An initial reserve without a policy returns 422 on `PolicyId` (BR-C-06).
6. **Unknown ids.** An unknown id in the URL gives 404. An unknown id in the body gives 422 ("Policy was not found.", "User was not found.").
7. **Responses.**
   - Create: 201 with a Location header and `ClaimCreatedDto` (id, number, status, validation issues, initial reserve with its warnings).
   - Status: 200 with `{claimId, previousStatus, status}`.
   - Add party / add risk object: 201 with no Location header, because there is no single-item GET.
   - Validate: 200 with every issue. Acknowledge: 200 with the issue.
   - Link policy, assignee, PATCH, remove party: 204.
8. **PATCH /claims/{id}.** A null field is left unchanged. An empty `notes` string clears the notes.
9. **Claims list.**
   - `dateFrom` and `dateTo` filter on the loss date's UTC calendar date, inclusive at both ends (D-32).
   - `search` matches part of the claim number or of the client name.
   - Sort order: ReportedDate descending, then ClaimNumber descending.
   - Paging: `page` ≥ 1, `pageSize` 1–100; anything else returns 422.
10. **Policy search.**
    - `q` is required (1–100 characters) and matches part of the policy number or client name.
    - At most 20 results, ordered by number.
    - The query is named `ListPoliciesQuery`, because FRS §15.3 allows only Get…/List… query names.
11. **Cause codes are compared ordinally.** SQL Server compares case-insensitively, but a code is an exact identifier, so "col-fire" is not
    recognised (BR-C-05).
12. **Idempotency refinements to D-24.**
    - Only 2xx responses are stored; any other outcome releases the key, so the client may retry. D-24 said "5xx not stored".
    - Keys are 1–200 characters.
    - Only authenticated POST/PUT/PATCH/DELETE requests take part.
    - The table gains a `ResponseLocation` column, and a replay carries `Idempotency-Replayed: true`, which CORS exposes.
    - The request hash is SHA-256 over the method, path, query and body.
    - An execution-strategy retry of our own insert is recognised by its record id.
    - The 24h clean-up job comes in Phase 4.
13. **Column sizes live in `Domain/Common/FieldLengths`**, shared by the EF configurations and the validators. An over-long value returns 422,
    never a SQL truncation error (500). The model and migrations are unchanged.
14. **Audit JSON.**
    - Values are camelCase objects.
    - STATUS_CHANGED: Old `{status}`, New `{status, reason}`.
    - CLAIM_CLOSED New: `{closureReason, justification, openReserveTotal}`.
    - RESERVE_REJECTED puts the reason in OldValue, as FRS §14.1 says literally, and in NewValue as well.
    - RelatedEntityId is null for claim-level events and names the child otherwise.
15. **Audit reads.** The detail includes the latest 10 entries. The audit endpoint pages by 50, with a maximum of 200 (D-33). Order is CreatedAt
    descending, then Id descending; ids are sequential (D-30), which keeps the rows of one transaction in write order.
16. **`GET /reference/claim-statuses`** returns every row, including the system-only Reopened → Open with `isSystemOnly: true`, so a UI menu can
    leave it out.
17. **`GET /users?role=`** matches one role exactly and returns the active users of the caller's organisation.

**Rationale.** Each item is small and reversible, and each is covered by a test named after its rule, so none is a silent choice.
**Status:** PROPOSED (2026-09-28). Q1 and Q2 were decided by Vlad; items 1–17 await review.

