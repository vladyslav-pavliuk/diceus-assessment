# AI log — Phase 0 (analysis)

- **Date:** 2026-09-27
- **Tool:** Claude Code (desktop app), model Claude Opus 5.5, Plan mode first, then execution.
- **Context loaded:** `CLAUDE.md`, `docs/spec/claims-frs.md` (read in full), `docs/spec/assessment-brief.md` (read in full), `docs/PROMPTS.md`.

## 1. Prompt given (verbatim: the KICKOFF PROMPT from `docs/PROMPTS.md`)

```
You are my senior engineering partner on a technical assessment for DICEUS, a company that builds
insurance Policy Administration Systems. I am a senior .NET/Angular engineer, and I own every decision. You are an
implementation and reasoning partner, not an autonomous author. I will be asked to defend every line of this code in
a 90-minute live review, and I will have to modify it live. Optimise for correctness, consistency and explainability,
not volume.

CONTEXT TO LOAD (read fully before answering):
- CLAUDE.md: the working agreement, fixed stack, architecture rules, and domain cheat-sheet.
- docs/spec/claims-frs.md: the Functional Requirements Spec. This is the DETAILED source of truth for business
  rules, entities, API behaviour, frontend behaviour, jobs, and conventions.
- docs/spec/assessment-brief.md: the assessment brief. It is authoritative on stack, Clean Architecture project
  layout, required endpoints, deliverables (README, ARCHITECTURE.md, AI-WORKFLOW.md, Azure, CI/CD), and evaluation
  criteria (§6). Read §5 "AI Workflow Expectations" and §7 "Live Technical Review" carefully, because they shape how we
  work, not just what we build.

WHAT TO BUILD
A greenfield vertical slice: FNOL intake → claim status state machine → parties/risk objects → event-sourced
reserves with 3-tier authority approval → Hangfire GL-posting simulation (idempotent) + SLA monitoring job →
document upload (Azure Blob / local fallback, SAS retrieval) → append-only audit log. Angular frontend with Claims
List, a 3-step FNOL form, and a 5-tab Claim Detail. Deployed on Azure with a GitHub Actions pipeline. Priority if
time is short (brief §8.3): a correct backend first, then deployment, then docs, then frontend polish.

PHASE 0 — ANALYSIS ONLY. Do not write application code yet.
Produce these files:

1. docs/REQUIREMENTS-MATRIX.md
   A table with one row per requirement: ID (BR-C-01…, BR-R-01…, BR-ST-*, BR-D-*, BR-P-*, BR-A-*, CC-01..04,
   plus the endpoint, job, and screen requirements from FRS §10–13 and brief §3.3–3.7). Columns: requirement, source
   (FRS §/brief §), layer(s) that enforce it (Domain / Validator / Handler / DB constraint / UI), planned test name,
   status (Planned). This matrix is our definition of done and the checklist for the final review.

2. docs/DECISIONS.md
   Every conflict, gap, or ambiguity between the two documents. For each one: context, options, your
   recommendation, and a rationale. Mark each as PROPOSED. I will accept or change them. At minimum, analyse these
   items I already found (verify them yourself and look for more):
   D-01 SLA job: the brief says "flag with SlaBreached status", while the FRS says "do not change claim status".
        (Recommend: audit entry + a separate IsSlaBreached/LastSlaBreachAt field. Status stays untouched.)
   D-02 Rule numbering clash: BR-C-06 means "no-policy warning" in the FRS and "Draft cannot go directly to
        Closed" in the brief. Also, BR-R-01..07 numbering differs between the two. (Recommend: FRS IDs are canonical,
        and brief-only rules get B-prefixed IDs.)
   D-03 Reserve component names: the brief's Appendix A.2 (IndemnityReserve/ExpenseReserve/RecoveryReserve/
        LitigationReserve) vs the FRS (Indemnity/Expense/ALAE/SubrogationRecoverable). (Recommend: the FRS names.)
   D-04 Reserve adjust API: the brief has PUT /reserves/{reserveId}, while the FRS has POST /reserves with
        transactionType plus a /retract endpoint. (Recommend: implement the FRS contract. Also add PUT as an
        "Adjust" transaction on an existing component so both contracts hold.)
   D-05 Negative amounts: BR-R-01 says amount > 0 except for SubrogationRecoverable, but ReserveHistory.Amount is a
        signed delta and TransactionType includes Adjust/Reverse. How is a reserve decreased? Which sign does the
        authority threshold use?
   D-06 Validation at intake: FRS §5.4 says a Critical issue leaves the claim in Draft (the claim is created), but
        §8/§10.4 show a 422 for a future loss date, and CauseOfLossCode is a NOT NULL FK, so an invalid code cannot be
        persisted. Decide which Critical rules reject the POST (422) and which are persisted as issues on a Draft
        claim (e.g. "no claimant" must be persistable, because parties can be added later).
   D-07 A ClaimValidationIssues entity is implied (VALIDATION_ISSUE_ADDED, CC-02 "unresolved Critical issues",
        §4.2 "or all waived", BR-C-02 warnings "cleared or acknowledged") but is not in the entity list. Design it.
        Specify who can acknowledge warnings and who can waive criticals.
   D-08 Missing endpoints implied by the UI and rules: POST /claims/{id}/validate, link/change policy
        (BR-C-06), assign handler (Open requires a handler), edit claim notes (Tab 1), manager sets the reserve-limit
        override flag (BR-R-05), retry a Failed GL posting (UI retry button), acknowledge or waive validation issues,
        GET /api/policies/{id}/coverage (brief only), dev token issuance for the mock auth.
   D-09 Who may transition status: the roles table says supervisor "manages status transitions", while §4.2 says
        "handler transitions". Model this per transition in the ClaimStatusTransitions.RequiredRole column.
   D-10 Claim number: "no gaps" vs "a SEQUENCE is acceptable". Sequences can gap on rollback or restart.
        (Recommend: a counter table updated inside the create transaction.)
   D-11 CurrentAmount = sum of "posted/approved" transactions: does it include Approved rows that are not yet Posted?
        Does PendingApproval count toward the $10M aggregate check?
   D-12 Tenant column name: OrganisationId (FRS) vs OrganizationEntityId (brief). Pick one and document it.
   D-13 The brief's Claims key fields include ClaimType and CauseOfLossCode, while the FRS puts CauseOfLossCode on
        LossEvents and has no ClaimType. Decide whether to denormalise.
   D-14 ClaimAuditLog vs the "soft delete on all tables" convention: an append-only log must not be soft-deletable.
   D-15 Enqueue GL job after commit (domain event dispatch timing) vs a transactional outbox. Decide how far to go.
   D-16 Mock auth: the backend must validate real JWTs (the brief requires a Bearer interceptor). Plan seeded
        users (≥2 handlers, ≥2 supervisors, 1 manager, so self-approval and cross-approval can be demonstrated) and
        a dev token endpoint.
   D-17 "Lazy-loaded feature modules" (FRS §11.4) vs modern standalone Angular: use lazy loadChildren route files
        per feature and justify it.
   D-18 Draft → Open requires "handler set". Decide how and when AssignedHandlerId is set.

3. docs/ARCHITECTURE-PLAN.md (a draft that becomes ARCHITECTURE.md later)
   - Aggregates and boundaries: which entities are aggregate roots (Claim; is ReserveComponent part of the Claim
     aggregate or separate? Argue it, including concurrency via RowVer), value objects (Money, ClaimNumber,
     PartyName, …), enums, and the domain events list with their handlers (before-commit vs after-commit).
   - Data model: every table with key columns, FKs, indexes (unique ClaimNumber per org, unique IdempotencyKey,
     filtered indexes), and which tables get soft-delete, tenant filters, and RowVer.
   - CQRS flow for one command end to end (POST reserve): controller → ValidationBehavior →
     handler → aggregate → UoW commit → domain events → audit → Hangfire enqueue.
   - The MediatR pipeline behaviours you propose (Validation, Logging, Transaction/UoW?, Idempotency?) and their
     order.
   - Hangfire design: jobs, triggers, retry policy, idempotency, and failure handling.
   - Azure target architecture: App Service (API), Static Web App (UI), Azure SQL (serverless/free offer), Storage
     account, Key Vault + Managed Identity (user-delegation SAS when using managed identity; account-key SAS when
     using a connection string), App Insights (optional). Local dev: docker-compose with SQL Server 2022 + Azurite.
   - A phased implementation plan matching brief §5.2 (below) with an estimate per phase.

4. docs/ai-log/phase-0.md: log this prompt and a summary of your output.

Rules for this phase:
- Quote the section number for every claim you make about the spec. If the spec does not say something, write
  "NOT SPECIFIED" rather than inventing a requirement.
- Where you are making an assumption, say so explicitly.
- Stop when done and give me a short summary: the top 5 risks, the decisions I must make first, and anything in the
  spec you think I have misunderstood.
```

## 2. How Claude worked
1. Read both specs end to end, plus PROMPTS.md, before producing anything.
2. In Plan mode, wrote a plan containing the substantive analysis (all decision recommendations, new conflicts found, matrix ID scheme, architecture
   outline). Vlad approved the plan without edits.
3. Wrote the Phase 0 files. No application code was written and nothing was built (there is nothing to build yet).

## 3. What was generated
| File | Content |
|---|---|
| `docs/REQUIREMENTS-MATRIX.md` | ~200 rows: FRS BR-*/CC-*, B-C-06 (brief-only), TR-00..13 transitions, VAL-01..14 (verbatim §8 messages), RSV-*, API-01..29 + cross-cutting API rows, JOB-01..12, DOC-01..08, AUD-01..17 + integrity rows, SEC-*, CONV-01..15, UI-*, DEL-01..10. Each row has a source, enforcing layer(s) and a planned test name. All rows Planned. |
| `docs/DECISIONS.md` | D-01..D-37, all PROPOSED. D-01..D-18 are the items Vlad listed (verified against the text, some adjusted). D-19..D-37 are new findings. |
| `docs/ARCHITECTURE-PLAN.md` | Aggregate boundary argument, VOs, enums, event→handler table (before/after commit), full table/index matrix, end-to-end CQRS flow for POST reserve, pipeline order, Hangfire design, Azure target, phase estimates. |
| `docs/REVIEW-PREP.md` | Seed list of likely reviewer questions with short answers. |
| `docs/ai-log/phase-0.md` | This file. |

## 4. Where Claude disagreed with or refined the prompt's suggested recommendations
These are recorded so the AI-WORKFLOW report can show *reasoned* pushback. They are Claude's claims and still await Vlad's acceptance.
- **D-01:** the prompt suggested `IsSlaBreached/LastSlaBreachAt` columns on Claims. Claude flagged that writing them bumps `UpdatedAt` through the audit
  interceptor, which resets the very 48h clock the job measures, and also bumps RowVer (spurious 409s). Counter-proposal: derive the breach from the audit log.
  Also: `UpdatedAt` is nullable, so the query needs `COALESCE(UpdatedAt, CreatedAt)`.
- **D-06:** the prompt assumed "no claimant must be persistable". Claude pointed out that FRS §10.4's own example 422 contains a `ClaimParties` error, and brief
  BR-C-03 is unqualified, so rejecting on POST is also defensible. The hybrid is still recommended.
- **D-16:** the prompt proposed 1 manager. Claude showed that a manager's own >$100k transaction could then never be approved, and proposed 2 per role.
- **D-07:** Claude recommended *not* building "waive", because under D-06 the only persisted Critical (no claimant) is also an independent hard condition.
- **CLAUDE.md "Reopened → Open writes two STATUS_CHANGED":** correct, but §14.1 also requires CLAIM_REOPENED, so a reopen writes 3 rows (D-26).
- **CLAUDE.md "never UPDATE ReserveHistory":** the FRS requires updating approval and posting fields on the row. Refined to "amount-immutable, not
  row-immutable" (D-22).

## 5. Self-corrections during generation (honest record)
- In the first draft of the matrix, Claude gave the "cannot remove the last Claimant" rule the ID **BR-P-03**. That ID does not exist in the FRS, and it
  breaks D-02's rule that the `BR-` namespace belongs to the FRS. Claude caught this on review and renamed it **PTY-01**.
- The matrix referenced a note "recorded under D-02" about the FRS's two different no-policy messages (§7.1 vs §8) before D-02 contained it. Claude added it to D-02.
- The planned grep cross-check found `BR-CL-01`, which FRS §4.2 references but never defines. Claude had missed it. It is now recorded in D-02 and the matrix mapping, together with the §4.1 "see Section 7.6" dangling reference.

## 6. What Vlad changed or rejected
Vlad's follow-up prompt (verbatim):
> Use azure container apps to reduce db costs.
> Apply the most logical and optimal options for the decisions, I'll adjust the functionality or business rules after we build and deploy the first version of the app.

- **D-36 changed.** *Before:* Claude recommended App Service B1 (Always On) + Azure SQL Basic 5 DTU (~USD 18/month), prioritising a warm demo. *After:* Azure
  Container Apps (Consumption, `minReplicas = 0`) + Azure SQL serverless free offer with auto-pause. Scale-to-zero stops Hangfire polling, so the database can pause.
  Claude documented the consequences: cold start, SLA runs only while a replica is up, a `minReplicas = 1` runbook step for the live review, GHCR instead of ACR, and
  the brief §3.8 "App Service" wording vs §2.3 "or Container Apps".
- **D-01..D-35, D-37:** accepted as Claude recommended them. Vlad delegated these choices explicitly and will revisit business rules after the first deployment.
  Where a decision offered an alternative, the recommended option was taken: D-06 hybrid, D-07 no waive, D-11 gross aggregate + Manager escalation,
  D-16 dev-token with 2 users per role, D-22 one pending per component.
- CLAUDE.md's stack line was updated from "Azure App Service" to "Azure Container Apps" to keep the session context consistent with D-36.

## 7. What Claude got wrong
_To be filled in from Vlad's review. Do not invent entries._
