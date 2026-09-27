**DICEUS**

Engineering Excellence Programme

Fullstack Engineer

**Technical Assessment**

.NET 9 + Angular · AI-Native Enterprise Delivery

**Claims Module — FNOL & Reserve Management**

Greenfield Vertical Slice Implementation

CONFIDENTIAL — FOR CANDIDATE USE ONLY

Version 1.0 · May 2026

# 1 Assessment Overview

## 1.1 Purpose

This assessment is the primary technical evaluation for candidates
applying to a Fullstack Engineer role at DICEUS. It simulates real
enterprise software delivery work within the domain of insurance
technology — specifically the Claims Management module of a Policy
Administration System (PAS).

The objective is not to test algorithmic puzzle-solving or isolated
unit-of-work coding speed. The objective is to evaluate whether you can
independently deliver a meaningful, production-oriented vertical slice
of enterprise software: from database schema through backend CQRS
architecture to a functional Angular frontend — deployed and running on
Azure.

## 1.2 Expected Candidate Level

This assessment is designed for engineers who:

- Have 4+ years of professional experience with .NET backend development
  > and Angular frontend development

- Have worked on enterprise-grade applications with real layering,
  > validation, and workflow requirements

- Are comfortable owning a feature end-to-end: DB schema, API, business
  > logic, and frontend

- Treat AI-assisted development as a standard, amplifying workflow — not
  > a shortcut to avoid reasoning

- Can deploy and operate a cloud-hosted application independently on
  > Azure

## 1.3 Evaluation Philosophy

**Architecture and judgment matter more than boilerplate output.**
DICEUS engineers routinely use AI tooling to accelerate delivery. The
expectation is that you will use Claude, Cursor, Copilot, or equivalent
tools heavily during this assignment. What we evaluate is not how many
lines you typed — it is whether the output reflects genuine engineering
ownership: thoughtful architecture, consistent design decisions, proper
validation, and a system that behaves correctly.

A candidate who uses AI well — who prompts with rich context, critically
reviews generated output, refines incorrect results, and produces
coherent software — demonstrates the exact skills that drive delivery
effectiveness at DICEUS.

## 1.4 Engineering Mindset Expected

Approach this assignment the way you would approach the first sprint of
a new enterprise module at DICEUS:

- Read the business requirements carefully before designing anything

- Design the data model before writing application code

- Make architectural decisions explicitly — name your patterns, explain
  > your layering

- Treat AI as a powerful implementation partner, not an autonomous
  > author

- Validate what AI generates — test it, refine it, own it

- Document your reasoning, especially where you made non-obvious
  > tradeoffs

# 2 Technical Context

## 2.1 Business Domain Background

DICEUS builds enterprise Policy Administration Systems (PAS) for
insurance carriers, captives, MGAs, and Lloyd's syndicates. A PAS
manages the full insurance lifecycle: product configuration, policy
issuance, endorsements, billing, and claims.

The Claims Management module is one of the most complex components of a
PAS. A claim represents a formal request by an insured for
indemnification following a covered loss event. The claims lifecycle
spans: First Notice of Loss (FNOL) intake, triage and assignment,
coverage verification, reserving, investigation, payment, and closure.

The provided FRS (Functional Requirements Specification) and DDL
describe the full claims module as an extension of an existing PAS. For
this assessment, you will implement a bounded greenfield subset of that
domain — described in Section 3.

## 2.2 Greenfield Implementation Assumptions

**Critical:** You will NOT have access to any DICEUS codebase,
libraries, infrastructure, or services. You start from zero. You
independently design and implement everything. The provided FRS and DDL
are domain specifications — they describe the business problem. Your
implementation architecture, project structure, code organization, and
design decisions are entirely yours.

You should treat the FRS as a business analyst's requirements document.
You are the architect and engineer who must translate those requirements
into a working system.

## 2.3 Required Technology Stack

|                          |                                                          |
|--------------------------|----------------------------------------------------------|
| **Runtime**              | .NET 9 / C# 13                                           |
| **Web API**              | ASP.NET Core Web API                                     |
| **CQRS / Mediator**      | MediatR 12+                                              |
| **ORM**                  | Entity Framework Core 9                                  |
| **Validation**           | FluentValidation                                         |
| **Mapping**              | AutoMapper                                               |
| **Background Jobs**      | Hangfire                                                 |
| **Database**             | SQL Server 2022 (or Azure SQL)                           |
| **Serialization**        | System.Text.Json / Newtonsoft.Json                       |
| **File Storage**         | Azure Blob Storage                                       |
| **Frontend Framework**   | Angular 18+                                              |
| **UI Component Library** | Angular Material (or equivalent)                         |
| **Forms**                | Reactive Forms                                           |
| **State Management**     | Candidate's choice (NgRx, signals, services)             |
| **Architecture Pattern** | Clean Architecture                                       |
| **Deployment**           | Microsoft Azure (App Service or Container Apps)          |
| **Source Control**       | Git (public or shared repository)                        |
| **CI/CD**                | GitHub Actions or Azure DevOps (basic pipeline required) |

## 2.4 Architecture Expectations

The backend solution must follow Clean Architecture with the following
project structure:

- **ClaimsModule.Domain** — entities, value objects, domain events,
  > enumerations

- **ClaimsModule.Application** — MediatR commands/queries, validators,
  > DTOs, interfaces, AutoMapper profiles

- **ClaimsModule.Infrastructure** — external services (Azure Blob,
  > email, etc.), Hangfire job definitions

- **ClaimsModule.Persistence** — EF Core DbContext, migrations,
  > repositories, Unit of Work

- **ClaimsModule.API** — ASP.NET Core controllers, middleware,
  > configuration, startup

CQRS must be implemented through MediatR. Every state-changing operation
must be a Command; every read operation must be a Query. Domain Events
should be raised for significant state transitions. The Unit of Work
pattern must coordinate persistence.

# 3 Candidate Assignment

## 3.1 Selected Scope: FNOL Intake + Reserve Management

From the full Claims module, you are required to implement the following
vertical slice:

|           |                                                                                                                                                                                                                                                         |
|-----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **SCOPE** | First Notice of Loss (FNOL) Intake — Claim creation, policy lookup, risk linkage, validation, status workflow — plus Reserve Component Management — opening reserves, reserve changes, authority validation, and Hangfire-driven GL posting simulation. |

This scope is chosen because it is architecturally rich: it requires
multi-step workflow, domain validation, authority-level business rules,
background job orchestration, meaningful frontend state, and end-to-end
ownership. It is feasible within a bounded timebox while still
demonstrating genuine enterprise engineering competence.

## 3.2 Database Requirements

You must design and implement a SQL Server schema that covers, at
minimum, the following entities. You should derive these from the
provided DDL — you are NOT required to copy it verbatim. Design your own
schema that satisfies the functional requirements described below.

|                        |                                          |                                                                     |
|------------------------|------------------------------------------|---------------------------------------------------------------------|
| **Entity**             | **Purpose**                              | **Key Fields**                                                      |
| Claims                 | Core claim aggregate root                | ClaimNumber, PolicyId, LossDate, Status, ClaimType, CauseOfLossCode |
| LossEvents             | The loss occurrence linked to a claim    | LossDate, LossLocation, LossDescription, CauseOfLossCode            |
| ClaimParties           | Claimants, witnesses, third parties      | PartyType, FirstName, LastName, ContactInfo                         |
| ClaimRiskObjects       | Risk assets linked to the claim          | InsuredAssetType, AssetReference, DamageDescription                 |
| ClaimReserveComponents | Financial reserve line per coverage type | ComponentType, CurrentAmount, Status, ApprovalStatus                |
| ReserveHistory         | Audit trail of reserve changes           | PreviousAmount, NewAmount, ChangeReason, ChangedBy                  |
| ClaimDocuments         | Files attached to the claim              | FileName, BlobReference, DocumentType, UploadedAt                   |
| ClaimAuditLog          | Immutable event log for the claim        | EventType, OldValues, NewValues, TriggeredBy                        |
| CauseOfLossCodes       | Lookup table: cause codes                | Code, Description, PerilCategory                                    |
| ClaimStatusTransitions | Allowed workflow transitions             | FromStatus, ToStatus, RequiredPermission                            |

All tables must follow the conventions from the FRS: UNIQUEIDENTIFIER
PKs, DECIMAL(19,4) monetary fields, DATETIMEOFFSET(7) timestamps, soft
delete columns, full audit columns, and tenant isolation via
OrganizationEntityId. EF Core migrations must be used — no raw SQL for
schema creation at runtime.

## 3.3 Backend API Requirements

The following REST API endpoints must be implemented. Each command/query
must be implemented as a MediatR handler. FluentValidation validators
must be defined for all commands.

### 3.3.1 FNOL / Claim Management

|                                 |                                           |                                                     |
|---------------------------------|-------------------------------------------|-----------------------------------------------------|
| **Method + Route**              | **Description**                           | **Notes**                                           |
| POST /api/claims                | Create a new claim (FNOL)                 | Full validation; triggers ClaimCreated domain event |
| GET /api/claims                 | List claims with filtering and pagination | Filter by status, date range, assignee, cause code  |
| GET /api/claims/{id}            | Get full claim detail                     | Includes parties, risk objects, reserves            |
| PUT /api/claims/{id}/status     | Transition claim status                   | Must validate against allowed transitions table     |
| GET /api/claims/{id}/audit      | Retrieve claim audit log                  | Immutable append-only log                           |
| POST /api/claims/{id}/documents | Upload document to claim                  | Stores file in Azure Blob; saves metadata           |
| GET /api/claims/{id}/documents  | List documents for claim                  | Returns signed URL for download                     |

### 3.3.2 Policy Lookup (Simulated)

Because this is a greenfield standalone implementation, you will
simulate the Policy domain with a minimal in-memory or seeded dataset.
You do not need to build a full Policy module.

|                                 |                                   |                                                                   |
|---------------------------------|-----------------------------------|-------------------------------------------------------------------|
| **Method + Route**              | **Description**                   | **Notes**                                                         |
| GET /api/policies/search        | Search policies by number or name | Returns PolicyId, PolicyNumber, ClientName, EffectiveDate, Status |
| GET /api/policies/{id}/coverage | Get coverages for a policy        | Used during FNOL to display available coverage types              |

### 3.3.3 Reserve Management

|                                                    |                                         |                                                                                 |
|----------------------------------------------------|-----------------------------------------|---------------------------------------------------------------------------------|
| **Method + Route**                                 | **Description**                         | **Notes**                                                                       |
| POST /api/claims/{id}/reserves                     | Open a reserve component                | Validate amount vs. authority levels; create pending approval if over threshold |
| PUT /api/claims/{id}/reserves/{reserveId}          | Adjust reserve amount                   | Creates ReserveHistory record; re-validates authority                           |
| GET /api/claims/{id}/reserves                      | List all reserve components for a claim | Include current amount, approval status, change history                         |
| POST /api/claims/{id}/reserves/{reserveId}/approve | Approve a pending reserve               | Requires elevated permission; triggers GL posting job                           |
| POST /api/claims/{id}/reserves/{reserveId}/reject  | Reject a pending reserve                | Records rejection reason                                                        |

### 3.3.4 Reference Data

|                                        |                                 |                                         |
|----------------------------------------|---------------------------------|-----------------------------------------|
| **Method + Route**                     | **Description**                 | **Notes**                               |
| GET /api/reference/cause-of-loss-codes | List active cause of loss codes | Filterable by peril category            |
| GET /api/reference/claim-statuses      | List valid claim statuses       | Includes allowed transitions per status |

## 3.4 Business Rules & Validation

The following business rules must be implemented as FluentValidation
rules on Commands and as domain logic on entities. Each rule must be
enforced server-side; the frontend should reflect validation results.

### 3.4.1 FNOL / Claim Creation Rules

- **BR-C-01:** Loss date must not be in the future.

- **BR-C-02:** Loss date must fall within the linked policy's effective
  > and expiration dates. If it does not, a validation warning is
  > returned — the claim can still be created but the warning is
  > recorded in the audit log.

- **BR-C-03:** A claim must have at least one ClaimParty of type
  > Claimant.

- **BR-C-04:** Claim number must be unique per organization. Use a
  > database sequence or equivalent atomic generation strategy. Format:
  > CLM-{YYYY}-{7-digit-zero-padded}.

- **BR-C-05:** CauseOfLossCode must exist and be active in the reference
  > table for the organization.

- **BR-C-06:** A draft claim may not transition directly to Closed.
  > Required path: Draft → Open → UnderInvestigation → PendingPayment →
  > Closed (or Draft → Open → Closed for trivial claims, if configured).

### 3.4.2 Reserve Management Rules

- **BR-R-01:** A reserve component amount must be greater than zero.

- **BR-R-02:** If the reserve amount is ≤ \$10,000, it is auto-approved
  > and a GL posting job is immediately enqueued.

- **BR-R-03:** If the reserve amount is \> \$10,000 and ≤ \$100,000, a
  > supervisor approval is required before GL posting.

- **BR-R-04:** If the reserve amount is \> \$100,000, a manager approval
  > is required before GL posting.

- **BR-R-05:** The GL posting job (Hangfire) must use an idempotency key
  > in the format: Reserve:{ReserveId}:Change:{ChangeSequence}. The job
  > must be re-entrant safe.

- **BR-R-06:** A rejected reserve may be re-submitted with a revised
  > amount. The revision creates a new pending record; the original
  > rejected record is retained in history.

- **BR-R-07:** Total approved reserves across all components for a claim
  > may not exceed \$10,000,000 without a manager override flag being
  > set.

## 3.5 Hangfire Background Jobs

The following Hangfire jobs must be implemented:

- **GL Posting Simulation Job:** Triggered after a reserve is
  > auto-approved or manually approved. Logs a structured entry in the
  > ClaimAuditLog simulating what would be a real GL journal entry: DR
  > Change in Outstanding Reserves / CR Outstanding Loss Reserves. The
  > job must be idempotent — running it twice with the same key must
  > produce no duplicate log entries.

- **SLA Monitoring Job (Recurring):** A recurring Hangfire job (every 15
  > minutes) that scans claims in Draft or Open status that have not
  > been updated in more than 48 hours and flags them with a SlaBreached
  > status. The job logs each flagged claim in the audit log with event
  > type SLA_BREACH_DETECTED.

## 3.6 Azure Blob Storage

Document upload must be integrated with Azure Blob Storage:

- Documents uploaded via POST /api/claims/{id}/documents must be stored
  > in Azure Blob Storage (container:
  > claim-documents/{organizationId}/{claimId}/)

- The API must return a short-lived SAS URL (1 hour TTL) for document
  > retrieval

- If Azure Blob Storage is unavailable during local development, the
  > implementation must gracefully fall back to local filesystem storage
  > using an IStorageService interface

- The storage provider must be configurable via appsettings.json
  > (Provider: AzureBlob \| LocalFileSystem)

## 3.7 Frontend Requirements

The Angular frontend must implement the following screens as a coherent,
functional UI. It must communicate with the backend API exclusively
through a typed Angular service layer. There must be no direct HTTP
calls outside the service layer.

### 3.7.1 Claims List (Dashboard)

- Paginated, filterable table of claims

- Filters: status, date range, assigned handler, cause of loss

- Each row shows: Claim Number, Policy Number, Client Name, Loss Date,
  > Status badge, Reserve Total

- Status badges must be color-coded (e.g., Draft = gray, Open = blue,
  > UnderInvestigation = orange, PendingPayment = purple, Closed =
  > green)

- Click on a row navigates to the Claim Detail screen

- "Log New Claim" button opens the FNOL intake form

### 3.7.2 FNOL Intake Form

- Multi-step reactive form with at minimum 3 steps: (1) Policy & Loss
  > Details, (2) Claimant & Party Details, (3) Initial Reserve & Review

- Step 1: Policy search (typeahead), loss date, cause of loss code, loss
  > description, loss location

- Step 2: Add claimant (at least one required), add additional parties
  > (optional), linked risk objects

- Step 3: Optional initial reserve entry with real-time authority
  > threshold indicator; review summary before submission

- Each step must validate independently before proceeding to the next

- Show inline validation messages using Angular Material form field
  > error patterns

- On submission, display success banner with generated Claim Number and
  > link to detail screen

### 3.7.3 Claim Detail

- Header section: Claim Number, Status chip, Policy Number, Loss Date,
  > Cause of Loss, Assigned Handler

- Tabs or sections: Overview \| Parties \| Reserves \| Documents \|
  > Audit Log

- Reserves tab: table of reserve components with type, amount, approval
  > status; action buttons per row (Approve / Reject — shown only for
  > eligible users); Add Reserve button opens inline form

- Documents tab: list of uploaded documents with download link; upload
  > button opens file picker

- Audit Log tab: reverse-chronological immutable event list (event type,
  > timestamp, user, description)

- Status transition button: shows contextually valid next statuses;
  > confirmation dialog before transition

### 3.7.4 General Frontend Requirements

- Angular routing with lazy-loaded feature modules

- JWT-based authentication simulation: a hardcoded mock auth service is
  > acceptable — the point is that the HTTP interceptor adds a Bearer
  > token to all API calls, and the UI shows a logged-in user context

- Role-based UI gating: Reserve approval buttons must only be visible to
  > users with the supervisor or manager role (simulate via the mock
  > auth service)

- Angular Material theming: consistent, professional appearance with a
  > custom primary color palette

- HTTP error handling: all API errors must be caught and displayed as
  > user-friendly snackbar notifications

- Loading indicators: all async operations must show a loading spinner
  > or skeleton

## 3.8 Azure Deployment Requirements

The deployed solution must be publicly accessible on Microsoft Azure.
The candidate is responsible for provisioning all Azure resources
independently.

**Minimum required Azure resources:** Azure App Service (backend), Azure
Static Web App or second App Service (frontend), Azure SQL Database or
SQL Managed Instance, Azure Blob Storage account, Azure Key Vault
(optional but demonstrates maturity).

A free tier or trial subscription is acceptable and encouraged. The
assessment does not require production-scale infrastructure — it
requires a working deployed application accessible from a public URL.

A basic CI/CD pipeline using GitHub Actions or Azure DevOps Pipelines
must be configured. The pipeline must at minimum: build the backend, run
EF migrations, build the frontend, and deploy both to Azure. The
pipeline does not need to be fully automated on every push — a manually
triggered pipeline is acceptable.

# 4 Deliverables

## 4.1 Source Code Repository

Provide access to a Git repository (GitHub, GitLab, or Azure DevOps)
containing:

- Full backend solution (.NET, Clean Architecture as specified)

- Full frontend Angular project

- EF Core migration files

- SQL seed scripts (cause of loss codes, reference data, simulated
  > policy data)

- docker-compose.yml or equivalent for local development (optional but
  > valued)

- CI/CD pipeline definition file (GitHub Actions YAML or Azure Pipelines
  > YAML)

- README.md with setup instructions (see 4.2)

## 4.2 Setup Instructions

The README.md must include:

1.  Prerequisites: required SDK versions, tools, environment setup

2.  Local development setup: step-by-step instructions to run the
    solution locally

3.  Environment variables / appsettings: all required configuration keys
    (with example values)

4.  Database migration: how to apply EF Core migrations and seed
    reference data

5.  Azure deployment: description of the deployed Azure resources and
    how to reproduce the deployment

6.  Application walkthrough: brief description of implemented features

## 4.3 Deployed Azure Application

Provide:

- **Public backend URL**: https://{your-app}.azurewebsites.net or
  > equivalent — Swagger/OpenAPI must be accessible

- **Public frontend URL**: https://{your-app}.azurestaticapps.net or
  > equivalent

- **Test credentials**: at least two user accounts — one with standard
  > handler permissions, one with supervisor permissions (for reserve
  > approval flows)

- The application must be running and accessible at the time of the live
  > review session

## 4.4 Architecture Documentation

Provide an ARCHITECTURE.md or equivalent document that includes:

- Solution structure diagram or written description

- Data model description: key entities and their relationships

- CQRS flow description: how a command flows from API through
  > Application to Persistence

- Domain events: which events are raised and how they are handled

- Hangfire job design: what jobs exist, how they are triggered, how
  > idempotency is ensured

- Azure architecture: which Azure services are used and how they
  > interact

- Key design decisions and tradeoffs: anything non-obvious you decided,
  > and why

## 4.5 AI Workflow Report

Provide an AI-WORKFLOW.md document that includes:

- Which AI tools you used (Claude, Cursor, Copilot, ChatGPT, etc.)

- How you structured your AI-assisted workflow (context loading
  > strategy, prompt sequencing, iteration approach)

- Examples of prompts you used — include at least 3 representative
  > prompts with their purpose

- What was AI-generated and what was manually designed or refined

- At least 2 specific examples where AI generated incorrect or
  > suboptimal output and how you corrected it

- Your honest assessment of which parts of the implementation benefited
  > most from AI assistance

- A link to or export of your Claude/Cursor/ChatGPT conversation history
  > (see 4.6)

## 4.6 AI Interaction History

Provide evidence of your AI-assisted workflow. Acceptable formats:

- Exported Claude conversation (Share link or exported text/PDF)

- Cursor chat history export or screenshots

- ChatGPT conversation export

- Equivalent evidence of substantive AI-assisted implementation

|          |                                                                                                                                                                                                                                                            |
|----------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **NOTE** | AI interaction history is not submitted to verify that you used AI — it is submitted because transparency in AI-assisted development is part of DICEUS engineering culture. We want to understand how you work with AI, not penalize AI usage in any form. |

# 5 AI Workflow Expectations

## 5.1 AI Usage is Encouraged and Expected

DICEUS engineers use AI-assisted development tools as a standard part of
their daily workflow. This assessment is designed assuming you will use
Claude, Cursor, GitHub Copilot, ChatGPT, or equivalent tools. There is
no penalty for AI usage — the evaluation rewards intelligent,
well-orchestrated AI-assisted delivery.

|               |                                                                                                                                                                                                                                                                                                                                    |
|---------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **IMPORTANT** | The primary AI workflow emphasis in this assessment is Claude-assisted software delivery: large-context reasoning, structured prompting, iterative refinement, and architectural orchestration. Claude is used extensively within DICEUS for enterprise module generation, code review, architecture reasoning, and documentation. |

## 5.2 Recommended AI Workflow Approach

The following is a suggested (not mandatory) workflow for using AI
effectively on this assignment:

### Phase 1 — Architecture & Design

- Load the FRS excerpt (FNOL + Reserve sections) as context into Claude

- Ask Claude to help you design the Clean Architecture project structure

- Ask Claude to reason through the domain model: which entities are
  > aggregate roots, which are value objects, which domain events make
  > sense

- Review and refine the output — Claude will make assumptions; validate
  > them against the FRS

### Phase 2 — Database Schema

- Provide the FRS entity descriptions and conventions to Claude

- Ask Claude to generate EF Core entity configurations and migrations
  > aligned with the conventions

- Validate: check that UNIQUEIDENTIFIER PKs, DECIMAL(19,4) monetary
  > fields, audit columns, and soft delete patterns are correctly
  > applied

### Phase 3 — Backend Implementation

- Use Claude or Cursor to scaffold MediatR Commands and Queries with
  > FluentValidation validators

- For each handler, provide the business rule context from the FRS so
  > the generated logic is domain-correct

- Implement Hangfire jobs with idempotency — this is a good area to test
  > AI reasoning on concurrency constraints

### Phase 4 — Frontend

- Use Claude to generate Angular component scaffolding, reactive form
  > definitions, and service layer

- The FNOL multi-step form is complex — use AI to generate the form
  > group structure then validate the validation logic manually

- Use Cursor for in-editor refinement of component logic

### Phase 5 — Review, Refinement & Deployment

- Review all AI-generated code against the business rules in Section 3.4

- Identify gaps, hallucinations, or domain-incorrect logic and correct
  > them

- Document corrections in your AI Workflow Report

- Use Claude to help draft ARCHITECTURE.md and AI-WORKFLOW.md

## 5.3 What We Are NOT Evaluating

To be explicit about what this assessment does not penalize:

- Using AI to generate boilerplate, entity classes, DTO definitions,
  > AutoMapper profiles

- Using AI to draft documentation

- Using AI to troubleshoot deployment issues

- The total volume of AI-generated code

**What we ARE evaluating:** whether the final system reflects your
engineering judgment. If AI generated something that doesn't make sense
architecturally, that is on you as the engineer who shipped it.
Validation, ownership, and reasoning are yours.

# 6 Evaluation Criteria

The assessment is evaluated across four dimensions. The weighting
reflects the DICEUS engineering priorities.

|                                    |            |                                                                                                                                                                                       |
|------------------------------------|------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Evaluation Area**                | **Weight** | **What Reviewers Look For**                                                                                                                                                           |
| Engineering Quality & Architecture | **35%**    | Clean Architecture adherence; separation of concerns; CQRS correctness; domain model quality; EF Core design; migration quality; validation completeness; code consistency and naming |
| AI-Assisted Delivery Effectiveness | **25%**    | Workflow orchestration maturity; evidence of intelligent prompting; quality of AI refinement; correction of AI errors; AI workflow documentation quality                              |
| Fullstack Ownership & Delivery     | **25%**    | Backend completeness; frontend completeness; Azure deployment success; CI/CD pipeline; DB design quality; end-to-end feature delivery                                                 |
| Domain & Product Thinking          | **15%**    | Accuracy of business rule implementation; edge-case handling; validation correctness; workflow state machine correctness; realistic enterprise reasoning                              |

## 6.1 Engineering Quality Detail

Reviewers will specifically check:

- Does the CQRS implementation correctly separate reads from writes via
  > MediatR?

- Are domain events raised and handled correctly (e.g., ClaimCreated
  > triggers audit log entry)?

- Is FluentValidation wired at the MediatR pipeline level (not just at
  > the controller)?

- Is EF Core configured with proper value conversions, shadow
  > properties, and global query filters (e.g., soft delete, tenant
  > isolation)?

- Is the Unit of Work pattern used to coordinate transactions across
  > repositories?

- Are AutoMapper profiles defined in the Application layer (not the API
  > layer)?

- Is the Hangfire idempotency key strategy correctly implemented?

- Is Azure Blob Storage wrapped behind an interface for testability?

## 6.2 What Differentiates a Strong Submission

Submissions that stand out typically demonstrate:

- A well-reasoned ARCHITECTURE.md that shows the candidate understands
  > not just what they built but why

- Evidence of AI correction: the AI workflow report shows specific
  > places where generated output was wrong and how the candidate fixed
  > it

- Domain model depth: entities that reflect insurance domain concepts
  > (reserve components, audit log events, claim lifecycle states)
  > rather than generic CRUD tables

- Error handling and logging: structured logging, consistent error
  > responses, and a middleware error handling pipeline

- Deployment quality: the Azure application actually works end-to-end,
  > Swagger is populated with correct schemas, and the frontend loads
  > without console errors

- Code that reads like it was written by a senior engineer — consistent,
  > named well, maintainable

# 7 Live Technical Review

## 7.1 Session Format

Following submission, shortlisted candidates are invited to a 90-minute
live technical review session conducted via video call with screen
sharing of the deployed application.

|                  |                                                                                     |
|------------------|-------------------------------------------------------------------------------------|
| **Duration**     | 90 minutes total                                                                    |
| **Format**       | Screen share of deployed application + code walkthrough                             |
| **Participants** | 2–3 DICEUS engineers                                                                |
| **Preparation**  | Have your repository, deployed application, and local development environment ready |

## 7.2 Session Agenda

7.  **Architecture Walkthrough (20 min):** Walk reviewers through your
    solution structure. Explain the Clean Architecture layering, how
    CQRS flows, how domain events work, and how the Hangfire jobs are
    designed. No slides needed — walk through the code.

8.  **AI Workflow Review (15 min):** Share your AI interaction history
    on screen. Walk reviewers through how you used AI: what context you
    loaded, how your prompts evolved, what AI got wrong, and how you
    corrected it.

9.  **Feature Demonstration (20 min):** Demonstrate the deployed
    application. Create a claim via the FNOL form, add a reserve,
    trigger the approval workflow, upload a document, and show the audit
    log. Reviewers will ask you to navigate to specific parts of the
    application.

10. **Deep Dive Questions (20 min):** Reviewers will ask focused
    technical questions about specific implementation decisions. Topics
    will include: EF Core configuration choices, CQRS handler design,
    Hangfire job idempotency, Angular reactive form design, Azure
    deployment architecture.

11. **Live Implementation (15 min):** Reviewers will ask you to
    implement or modify a small piece of functionality live. This may
    involve adding a new validation rule, a new query endpoint, or
    modifying a frontend component. You may use AI tools — in fact,
    using AI during this phase is encouraged.

## 7.3 What the Live Session Evaluates

The live session is the most important phase of the evaluation. It
evaluates:

- **Engineering ownership:** Can you explain every significant decision
  > in your implementation? Do you understand the code at the level
  > required to modify it live?

- **Architectural reasoning:** Can you articulate why you made specific
  > architectural choices? Can you explain tradeoffs (e.g., why you
  > chose a particular repository pattern, how you handled concurrency)?

- **AI workflow maturity:** Does your AI interaction history show
  > intelligent prompting and critical refinement, or does it show
  > copy-paste without reasoning?

- **Real-time problem solving:** When asked to implement something live,
  > can you structure the problem, use AI intelligently, and produce
  > correct output within the session?

## 7.4 Engineering Authenticity

DICEUS values transparency in engineering. The live review is not a trap
or a test of memorization — it is a professional conversation between
engineers. The session validates authenticity through genuine
engagement:

A strong candidate who used AI extensively and can explain every
decision in their code is exactly the profile DICEUS is hiring. A
candidate who cannot explain implementation decisions — regardless of
how polished the code appears — will not pass the live review.

Architecture reasoning is evaluated by asking questions like: "Why is
this a Command rather than a direct service call?", "How would you
extend this to support multi-currency reserves?", or "What would break
if you removed the Unit of Work here?". Correct answers come from
understanding, not memorization.

The live implementation task is designed to require creative
problem-solving in real time, not recall. Using AI during this phase —
as you would in a real workday — is entirely appropriate and expected.

# 8 Logistics & Submission

## 8.1 Timeline

|                            |                                                                     |
|----------------------------|---------------------------------------------------------------------|
| **Assessment Issued**      | Day 0                                                               |
| **Recommended Timebox**    | 5–7 business days of engineering effort (not elapsed calendar time) |
| **Submission Deadline**    | As communicated by your DICEUS recruiter                            |
| **Live Review Scheduling** | Within 3 business days of successful submission review              |

## 8.2 Submission Package

Submit the following to your DICEUS contact:

12. Git repository URL (with access granted to the reviewing team)

13. Deployed frontend URL

14. Deployed backend / Swagger URL

15. Test credentials (handler role + supervisor role)

16. Link to or attachment of AI interaction history export

## 8.3 Scope Management Guidance

|              |                                                                                                                                                                                                                                                                                                                       |
|--------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **ADVISORY** | If you are running short on time, prioritize: (1) backend completeness and correctness, (2) deployed Azure application, (3) architecture documentation. A fully working backend with a partially complete frontend and strong documentation is a stronger submission than a complete frontend with a shallow backend. |

The assessment is designed so that a strong engineer with effective AI
tooling can complete it within the recommended timebox. You are not
expected to implement the full Claims module — you are expected to
implement the specified vertical slice with depth and quality.

## 8.4 Questions

Technical clarification questions about the assessment scope are
welcome. Contact your DICEUS recruiter who will route questions to the
engineering team. We will not answer questions that are essentially
asking us to make architectural decisions for you — those decisions are
yours to make and document.

# Appendix A — FRS Reference Summary

The following tables summarize key FRS concepts relevant to your
implementation scope. These are condensed references — consult the full
FRS documents provided alongside this assessment for complete detail.

## A.1 Claim Status Lifecycle

|                        |                                                       |
|------------------------|-------------------------------------------------------|
| **Draft**              | Created but not yet validated or assigned             |
| **Open**               | Validated, assigned to handler — active investigation |
| **UnderInvestigation** | Formal investigation triggered (e.g., possible fraud) |
| **PendingPayment**     | Liability accepted; payment processing in progress    |
| **Closed**             | All activity complete; financials reconciled          |
| **Reopened**           | Previously closed claim re-activated                  |
| **Withdrawn**          | Claimant withdrew the claim                           |

## A.2 Reserve Component Types

|                       |                                                      |
|-----------------------|------------------------------------------------------|
| **IndemnityReserve**  | The estimated loss indemnity payment to the claimant |
| **ExpenseReserve**    | Allocated loss adjustment expenses (ALAE)            |
| **RecoveryReserve**   | Expected subrogation or salvage recovery (negative)  |
| **LitigationReserve** | Legal defense costs if claim is litigated            |

## A.3 Reserve Authority Thresholds (Assessment Default)

|                                       |                                                           |
|---------------------------------------|-----------------------------------------------------------|
| **≤ \$10,000**                        | Auto-approved — GL posting job enqueued immediately       |
| **\> \$10,000 and ≤ \$100,000**       | Supervisor approval required before GL posting            |
| **\> \$100,000**                      | Manager approval required before GL posting               |
| **\> \$10,000,000 (total per claim)** | Manager override flag required — system warning generated |

## A.4 Key Data Conventions

|                      |                                                               |
|----------------------|---------------------------------------------------------------|
| **Primary Keys**     | UNIQUEIDENTIFIER (NEWSEQUENTIALID() default)                  |
| **Monetary Amounts** | DECIMAL(19,4)                                                 |
| **Timestamps**       | DATETIMEOFFSET(7)                                             |
| **Soft Delete**      | IsDeleted BIT + DeletedAt DATETIMEOFFSET(7)                   |
| **Audit Columns**    | CreatedAt, UpdatedAt, UserCreated, UserModified on all tables |
| **Tenant Isolation** | OrganizationEntityId UNIQUEIDENTIFIER NOT NULL on all tables  |
| **Concurrency**      | RowVer ROWVERSION on aggregate roots                          |
| **Code Strings**     | NVARCHAR(50)                                                  |
| **Name Strings**     | NVARCHAR(255)                                                 |
| **Free Text**        | NVARCHAR(MAX)                                                 |

© 2026 DICEUS · This document is confidential and intended solely for
the named candidate · All rights reserved
