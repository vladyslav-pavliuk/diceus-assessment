# Claude Code prompt pack — DICEUS Claims Module

How to use:
1. Create an empty repo. Copy `CLAUDE.md` to the root, and copy `docs/spec/*.md` and this file into `docs/`.
2. Start Claude Code in the repo root. Use **Plan mode** (Shift+Tab) for Phase 0 and for the planning part of each
   later phase.
3. Paste the **Kickoff prompt**. After that, paste one phase prompt at a time. Review the output, correct it, and only
   then move on. Your corrections are the material for AI-WORKFLOW.md.
4. At the end of each phase, run `/export` to save the conversation into `docs/ai-log/` (evidence for §4.6).

---

## KICKOFF PROMPT (paste first)

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

---

## PHASE 1 — Solution skeleton & cross-cutting concerns

```
Phase 1. DECISIONS.md is accepted with my edits (re-read it). Build the solution skeleton only:
- The 5 src projects + 3 test projects with correct references (enforce the dependency rule; add an
  architecture test with NetArchTest or similar).
- Directory.Build.props (nullable, warnings-as-errors for new code, C# 13), central package management with pinned
  versions (MediatR 12.x, AutoMapper 14.x — confirm the licences).
- API: ProblemDetails exception middleware (422 shape exactly as FRS §10.4), correlation-id middleware,
  structured logging, Swagger with the JWT security scheme, health check, CORS for the SPA.
- Application: ValidationBehavior, LoggingBehavior, ICurrentUser, IUnitOfWork, IAuditLogService,
  IStorageService, IBackgroundJobScheduler abstractions. Use TimeProvider.
- Mock auth: seeded users (per D-16), POST /api/auth/dev-token issuing a signed JWT with sub, name, role, and
  org claims; signing key from config (Key Vault in Azure); role policies "Handler", "Supervisor",
  "Manager".
- docker-compose.yml: SQL Server 2022 + Azurite (+ the API, optionally).
List the requirement IDs this phase touches, write the tests, and run build + test. Stop and summarise.
```

## PHASE 2 — Domain model & persistence

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

## PHASE 3 — Application layer: FNOL, claims, reference data

```
Phase 3: MediatR commands/queries + FluentValidation + controllers for FNOL, claims, parties, status,
validation issues, policies, and reference data (FRS §10.1, §10.3 and the endpoints added in D-08).
- CreateClaimCommand: single transaction; atomic claim number; LossEvent, parties, risk objects, and optional
  initial reserve (reusing the reserve logic from Phase 4 — stub it behind the same domain method now);
  validation issues persisted per D-06/D-07; ClaimCreated → audit. 201 + Location header.
- ListClaimsQuery with all the FRS filters, pagination metadata, and totalReserves. Project in SQL
  (ProjectTo / Select), with no N+1 queries. GetClaimDetailQuery. GetClaimAuditQuery (reverse chronological, paged).
- TransitionClaimStatusCommand: 422 with valid next statuses or a blocking-conditions list; role checks from
  the transitions table; CC-04 justification; Reopened→Open.
- AddParty / RemoveParty (422 when removing the last Claimant).
- The validators' messages must match FRS §8 word for word.
- Integration tests through HTTP (WebApplicationFactory + Testcontainers) for the happy path and each 422 rule.
Challenge the spec where it is weak, but implement what DECISIONS.md says. Stop and summarise.
```

## PHASE 4 — Reserves, authority & Hangfire

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

## PHASE 5 — Documents

```
Phase 5: documents (FRS §7.4, §13). IStorageService with AzureBlobStorageService (Azure.Storage.Blobs;
SAS with a 1h TTL; user-delegation SAS when using DefaultAzureCredential) and LocalFileSystemStorageService (a local
download URL served by a dev-only endpoint). Selected via Storage:Provider. Upload: multipart, 50 MB limit, MIME
allowlist checked by content sniffing (not just the Content-Type header), filename sanitisation against path
traversal (write tests with "../", absolute paths, and unicode tricks), DOCUMENT_UPLOADED audit. List returns
fresh SAS URLs. Integration-test the Azure implementation against Azurite. Stop and summarise.
```

## PHASE 6 — Angular frontend

```
Phase 6: the Angular app in web/claims-ui (FRS §11, brief §3.7). Plan the component tree, routes, services,
and state first, and show me the plan before generating code.
- Standalone components with lazy-loaded route files: claims-list, fnol-intake, claim-detail. A typed
  ClaimsApiService, ReferenceApiService, and AuthService (the only places that use HttpClient). Interfaces
  mirror the backend DTOs exactly.
- A mock auth with a user/role switcher in the toolbar; an interceptor adds the Bearer token and the correlation
  id; an error interceptor maps ProblemDetails (incl. the 422 errors dictionary) to snackbars and form errors;
  a loading indicator on every async operation, with buttons disabled while pending.
- Claims List: server-side pagination, filters (status multi-select, loss-date range, handler, cause), colour-coded
  status badges exactly as in FRS §11.1, empty state, row click → detail, "Log New Claim".
- FNOL: a MatStepper with one FormGroup per step, each validated independently; policy typeahead (debounced,
  switchMap); an in-force badge that recomputes when the loss date or policy changes; the Unknown Policy toggle;
  a future-date validator; a 20-char counter; a FormArray for parties with a Claimant-required validator; a FormArray
  for risk objects with an advisory; a live authority indicator; a review summary; a confirm dialog on warnings;
  the success snackbar with the claim number → detail.
- Claim Detail: a header with a copyable claim number, a status chip, and a transition menu of valid next statuses
  (a pre-flight checklist for Closed); the 5 tabs per FRS §11.3, including reserve summary cards, a history table with
  +/- colouring, a slide-in Add Reserve panel, role-gated Approve/Reject, Retract for the submitter only, the GL badge
  with retry, document upload with progress, and a paged audit log.
- A custom Material M3 theme with its own primary palette. Usable at 1280px+. No console errors.
- Unit tests for the FNOL validators and the authority-indicator logic.
Stop after the plan, then again after each screen.
```

## PHASE 7 — Azure deployment & CI/CD

```
Phase 7: Azure + GitHub Actions (brief §3.8).
- infra/: Bicep (preferred) or a documented az CLI script for App Service (Linux), Static Web App, Azure SQL
  (free/serverless), Storage account + claim-documents container, Key Vault, and a managed identity with
  the correct RBAC (Storage Blob Data Contributor + Storage Blob Delegator; Key Vault Secrets User).
- .github/workflows/deploy.yml (workflow_dispatch acceptable): build + test the backend; build an EF migrations
  bundle and run it against Azure SQL; build Angular with the production API URL; deploy the API and the SWA.
  Use OIDC federated credentials rather than stored publish profiles if feasible.
- Swagger enabled in the deployed environment; CORS locked to the SWA origin; Hangfire storage on Azure SQL.
- A smoke-test script covering the brief §7.2 demo flow: create a claim → add a reserve over 10k as a handler →
  approve as a supervisor → the GL job posts → upload a document → the audit log shows everything.
Tell me exactly which steps I must run manually (subscription, secrets, federated credential). Stop and summarise.
```

## PHASE 8 — Review, documentation & live-review prep

```
Phase 8: final review. Act as a strict DICEUS reviewer using brief §6 and §6.1 as the rubric.
1. Walk the REQUIREMENTS-MATRIX row by row and verify each requirement against code AND a test. Mark Done,
   Partial, or Missing with evidence (file:line / test name). Do not mark anything Done without evidence.
2. Check the §6.1 list explicitly: MediatR read/write separation; domain events → audit; validation in the
   pipeline; value conversions, shadow properties, global filters (soft delete + tenant); UoW; AutoMapper in
   Application; Hangfire idempotency; storage behind an interface.
3. Report hallucinations or domain errors you find in our own code (wrong thresholds, ≤ vs <, missed roles,
   wrong messages), and fix them only after I approve.
4. Draft README.md (all six sections from brief §4.2), ARCHITECTURE.md (all items from brief §4.4, with a Mermaid
   solution + data-model diagram), and the AI-WORKFLOW.md skeleton (brief §4.5). Use only facts from docs/ai-log/
   and DECISIONS.md for the "what AI got wrong" section, and leave TODO markers where I must write it myself.
5. Finalise REVIEW-PREP.md: 25 likely deep-dive questions with concise, code-referenced answers, plus 5
   rehearsal "live implementation" tasks (e.g. a new validation rule, a new query endpoint, a new filter on the
   claims list, a multi-currency sketch) with the files to touch.
```

---

## Tips for the AI-WORKFLOW.md (25% of the grade)
- The brief rewards **visible critical refinement**. Every time you reject or fix Claude's output, one line in
  `docs/ai-log/phase-N.md` saves you work later. Good candidates that usually come up on their own:
  a SEQUENCE-based claim number (gaps), a check-then-act GL idempotency (race), validation in controllers instead of
  the pipeline, `DateTime.Now` in rules, off-by-one thresholds (≤ vs <), and a SLA job that changes status.
- Keep `CLAUDE.md` in the repo. It is your "context loading strategy" artefact, and the phase prompts show your
  "prompt sequencing".
- Export each session (`/export`) into `docs/ai-log/` as you go. That covers the interaction-history deliverable (§4.6).
