TECHNICAL ASSESSMENT SPECIFICATION

**Claims Management System**

**FNOL Intake & Reserve Management**

Functional Specification · Candidate Reference Document

|                      |                                          |
|----------------------|------------------------------------------|
| **Document Version** | 1.0                                      |
| **Date**             | May 2026                                 |
| **Purpose**          | Technical Assessment —Fullstack Engineer |
| **Domain**           | Insurance Claims Management              |
| **Status**           | For Implementation                       |

*This document is self-contained. No additional business documentation
is required to complete the assessment.*

# Contents

**1** Introduction & Business Context 3

**2** Functional Scope 3

**3** User Roles & Permissions 4

**4** Claim Lifecycle 4

**5** FNOL Intake Workflow 6

**6** Reserve Management Workflow 9

**7** Business Rules 11

**8** Validation Rules 14

**9** Required Data Entities 15

**10** API Behaviour 19

**11** Frontend Behaviour 21

**12** Background Processing 24

**13** Document Management 25

**14** Audit & Logging 25

**15** Data Conventions & Constraints 26

# 1 Introduction & Business Context

## 1.1 What This Document Is

This specification defines the functional requirements for a standalone
Claims Management System covering two core workflows: First Notice of
Loss (FNOL) intake and Reserve Management. It is written specifically
for a technical assessment and is fully self-contained — no external
documentation or existing system access is required or assumed.

## 1.2 Business Domain

In the insurance industry, a claim represents a formal request by a
policyholder (or a third party) for financial compensation following a
covered loss event. The lifecycle of a claim begins with the First
Notice of Loss (FNOL) — the initial report of the incident — and
progresses through investigation, reserving, payment, and eventual
closure.

Key domain concepts used throughout this document:

- **Policy:** A contract between an insurer and a policyholder. A policy
  has an effective date and an expiration date, and covers one or more
  insured assets against specific types of loss.

- **Loss Event:** The incident that caused the damage or injury. A loss
  event has a date, location, and cause. One loss event may give rise to
  one claim.

- **Claim:** The formal record created from a loss event. A claim links
  the loss event to the policy and the affected parties, tracks the
  financial liability (reserves), and drives the investigation and
  payment process.

- **Reserve:** A financial estimate of the expected cost of a claim.
  Reserves are set at intake and adjusted as the claim develops. Each
  reserve is linked to a cost component (indemnity, expenses, etc.) and
  must be approved if it exceeds a threshold.

- **Cause of Loss Code:** A standardised code identifying the type of
  peril (e.g., fire, theft, collision, flood) that caused the loss.

- **Claimant:** The party seeking compensation. May be the insured, a
  third party, or a beneficiary.

## 1.3 Scope of This Assessment

You are implementing a vertical slice of a Claims Management System
covering:

- FNOL Intake — creating a claim from a reported loss event, linking
  parties and risk objects, performing validation

- Claim Lifecycle — managing status transitions from Draft through
  Closed

- Reserve Management — setting and adjusting financial reserves with
  authority-controlled approval

- GL Posting Simulation — background jobs that simulate posting reserve
  changes to a general ledger

- Document Management — uploading and retrieving supporting documents
  via cloud storage

- Audit Logging — an immutable append-only event log for every
  significant action

|               |                                                                                                                                                                                                                                                 |
|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **IMPORTANT** | This is a greenfield standalone implementation. You are not connecting to any existing system. You will simulate the Policy domain with seeded data. Everything else — database, API, frontend, deployment — you design and build from scratch. |

# 2 Functional Scope

The following table defines what is in scope and out of scope for this
assessment.

|                            |                |                                                                |
|----------------------------|----------------|----------------------------------------------------------------|
| **Feature Area**           | **In Scope**   | **Notes**                                                      |
| FNOL / Claim Creation      | ✓ Required     | Multi-step intake, policy lookup, party entry, validation      |
| Claim Status Transitions   | ✓ Required     | Defined state machine; transition validation enforced          |
| Claim Parties              | ✓ Required     | Claimant required; additional parties optional                 |
| Claim Risk Objects         | ✓ Required     | Simplified — asset type + description, no deep asset hierarchy |
| Reserve Management         | ✓ Required     | Open, adjust, approve/reject; GL posting simulation            |
| Reserve Authority Workflow | ✓ Required     | Three-tier threshold: auto / supervisor / manager              |
| Document Upload            | ✓ Required     | Azure Blob Storage; SAS URL retrieval                          |
| Audit Log                  | ✓ Required     | Append-only event log per claim                                |
| Policy Lookup (Simulated)  | ✓ Required     | Seeded dataset; search by policy number or name                |
| SLA Monitoring Job         | ✓ Required     | Recurring Hangfire job flags stale open claims                 |
| Claims List Dashboard      | ✓ Required     | Paginated, filterable Angular table                            |
| FNOL Intake Form           | ✓ Required     | Multi-step Angular reactive form                               |
| Claim Detail Screen        | ✓ Required     | Tabs: Overview / Parties / Reserves / Documents / Audit Log    |
| Coverage Evaluation        | ✗ Out of Scope | Simplified — coverage types seeded as reference data only      |
| Claim Payments             | ✗ Out of Scope | No payment disbursement in this assessment                     |
| Subrogation / Recoveries   | ✗ Out of Scope |                                                                |
| Litigation Management      | ✗ Out of Scope |                                                                |
| Fraud / SIU Scoring        | ✗ Out of Scope |                                                                |
| Bulk Operations            | ✗ Out of Scope |                                                                |
| Communications / Messaging | ✗ Out of Scope |                                                                |
| Reinsurance                | ✗ Out of Scope |                                                                |

# 3 User Roles & Permissions

The system defines three roles. Your implementation must simulate
role-based access; a mock authentication service is acceptable.

|                   |            |                                                                                                                                  |
|-------------------|------------|----------------------------------------------------------------------------------------------------------------------------------|
| **Role**          | **Code**   | **Capabilities**                                                                                                                 |
| Claims Handler    | handler    | Create claims, manage parties, upload documents, open reserves within authority (≤ \$10,000), view all claim data                |
| Claims Supervisor | supervisor | All handler capabilities + approve/reject reserves up to \$100,000, manage claim status transitions                              |
| Claims Manager    | manager    | All supervisor capabilities + approve/reject reserves up to \$10,000,000, set override flag for claims exceeding aggregate limit |

Role enforcement is required in two places:

- Backend: reserve approval endpoints must validate the caller's role
  before permitting approval

- Frontend: approve/reject buttons on reserves must only appear for
  users with supervisor or manager role

# 4 Claim Lifecycle

## 4.1 Claim Status Values

A claim moves through a defined set of statuses. The status controls
which actions are permitted and which UI elements are active.

|                    |                                                            |                                                    |
|--------------------|------------------------------------------------------------|----------------------------------------------------|
| **Status**         | **Meaning**                                                | **Entry Condition**                                |
| Draft              | Claim created but incomplete or unvalidated                | Claim first saved; automatic                       |
| Open               | Validated; handler assigned; active investigation          | All critical validation passes; handler set        |
| UnderInvestigation | Active detailed investigation in progress                  | Handler manually transitions from Open             |
| PendingPayment     | Liability accepted; awaiting payment processing            | Handler transitions; at least one reserve approved |
| Closed             | All activity complete; financials reconciled               | All closure conditions met (see Section 7.6)       |
| Reopened           | Previously closed claim reactivated with documented reason | Supervisor initiates reopen with reason code       |
| Withdrawn          | Claimant withdrew the claim                                | Handler transitions; withdrawal reason recorded    |

## 4.2 Valid Status Transitions

The application must enforce the following transition rules. Any
transition not listed is invalid and must return a 422 error.

|                    |                    |                                                                                          |
|--------------------|--------------------|------------------------------------------------------------------------------------------|
| **From Status**    | **To Status**      | **Rules / Conditions**                                                                   |
| Draft              | Open               | No critical validation issues remain (or all waived); at least one claimant party exists |
| Open               | UnderInvestigation | Handler or supervisor; no additional conditions                                          |
| Open               | PendingPayment     | At least one reserve component with status Approved exists                               |
| Open               | Closed             | All closure conditions satisfied (see BR-CL-01)                                          |
| Open               | Withdrawn          | Withdrawal reason code provided                                                          |
| UnderInvestigation | Open               | Handler manually reverts                                                                 |
| UnderInvestigation | PendingPayment     | At least one approved reserve; liability determined                                      |
| UnderInvestigation | Closed             | All closure conditions satisfied                                                         |
| UnderInvestigation | Withdrawn          | Withdrawal reason provided                                                               |
| PendingPayment     | Closed             | All closure conditions satisfied                                                         |
| Closed             | Reopened           | Reopen reason code provided; supervisor role required                                    |
| Reopened           | Open               | Immediately on reopen                                                                    |

## 4.3 Closure Conditions

A claim may only transition to Closed when all of the following
conditions are satisfied. The API must check and return blocking issues
if any condition fails.

1.  **CC-01:** No reserve components with status PendingApproval remain.

2.  **CC-02:** There are no unresolved Critical validation issues
    recorded against the claim.

3.  **CC-03:** At least one ClaimParty of role Claimant exists on the
    claim.

4.  **CC-04:** If any reserve component has a CurrentAmount \> 0 at the
    time of closure, a non-critical warning is returned. The handler
    must explicitly confirm closure with open reserves by providing a
    justification note.

# 5 FNOL Intake Workflow

## 5.1 Overview

FNOL (First Notice of Loss) is the process of formally recording a claim
when a policyholder or third party reports a loss event. The FNOL
process results in the creation of a Claim record along with its
associated loss event details, parties, and optional initial reserve.

The intake process validates completeness and business rules. Validation
issues are categorised as Critical (blocking) or Warning (non-blocking).
A claim cannot transition from Draft to Open while any Critical issue
remains unresolved.

## 5.2 Intake Steps

The FNOL intake is structured in three logical steps. All data may be
submitted in a single API call; the step structure is a frontend UX
concern.

### Step 1 — Policy & Loss Details

- User searches for a policy by policy number or client name

- System returns matching policies from the seeded dataset (see Section
  5.5)

- User selects the policy; system displays policy effective dates and
  available coverage types

- User enters: Loss Date, Loss Description, Cause of Loss Code, Loss
  Location (free text), optionally Estimated Loss Amount

- If policy cannot be identified, user may proceed without linking a
  policy (the claim is created with PolicyId = null and status flagged
  with a Warning: Policy Unknown)

### Step 2 — Parties & Risk Objects

- User adds at least one ClaimParty with role Claimant (required before
  Open transition)

- User may add additional parties: Insured, Witness, ThirdParty,
  Attorney

- User adds one or more Risk Objects: describes the damaged asset by
  type and description

- Asset types supported: Vehicle, Property, Person, Equipment, Other

### Step 3 — Initial Reserve & Review

- User optionally enters an initial reserve amount and component type

- System displays authority threshold preview: will this require
  approval?

- User reviews the full intake summary before submitting

- On submission: Claim record created, ClaimNumber generated, all child
  records created transactionally

- On success: user is navigated to the Claim Detail screen with a
  success banner showing the generated Claim Number

## 5.3 Claim Number Generation

Claim numbers must be unique per organisation and generated atomically.
No gaps are permitted; no reuse even if a claim is soft-deleted.

Format: CLM-{YYYY}-{7-digit-zero-padded-sequence} e.g. CLM-2026-0000142

Implementation must use an atomic database-level counter increment
(UPDATE … OUTPUT INSERTED pattern or equivalent) to prevent duplicate
numbers under concurrent submissions. A database SEQUENCE object or a
dedicated sequence table with optimistic concurrency is both acceptable.

## 5.4 Validation at Intake

Validation runs synchronously on the create endpoint and may also be
called explicitly via the validate endpoint. Results are categorised:

|              |                                |                                                                                    |
|--------------|--------------------------------|------------------------------------------------------------------------------------|
| **Severity** | **Blocks Transition?**         | **Examples**                                                                       |
| Critical     | Yes — claim stays in Draft     | Loss date in the future; invalid cause of loss code; no claimant party             |
| Warning      | No — claim may proceed to Open | Policy not found; loss date outside policy effective dates; no risk objects linked |

## 5.5 Simulated Policy Data

Because this is a standalone greenfield system, you must seed a minimal
dataset of simulated policies. The following seed data is required as a
minimum; you may add more.

|                   |                           |                    |                     |                     |
|-------------------|---------------------------|--------------------|---------------------|---------------------|
| **Policy Number** | **Client Name**           | **Effective Date** | **Expiration Date** | **Coverage Types**  |
| POL-2024-001001   | Meridian Transport LLC    | 2024-01-01         | 2026-12-31          | Vehicle, Cargo      |
| POL-2024-001002   | Harborview Properties Inc | 2024-06-01         | 2026-05-31          | Property, Liability |
| POL-2025-002001   | Coastal Builders Group    | 2025-03-01         | 2027-02-28          | Property, Equipment |
| POL-2025-002002   | Stanton Medical Group     | 2025-01-01         | 2026-12-31          | Liability, Vehicle  |
| POL-2023-000099   | Archived Corp             | 2020-01-01         | 2021-12-31          | Property            |

The last entry (POL-2023-000099) has an expired policy — it is used to
test the validation rule that generates a Warning when the loss date
falls outside the policy effective period.

## 5.6 Cause of Loss Reference Data

The following cause of loss codes must be seeded as active reference
data:

|              |                       |                    |                                                 |
|--------------|-----------------------|--------------------|-------------------------------------------------|
| **Code**     | **Name**              | **Peril Category** | **Notes**                                       |
| COL-FIRE     | Fire                  | Property           | Structure or contents fire                      |
| COL-FLOOD    | Flood                 | Weather            | Water intrusion from external source            |
| COL-THEFT    | Theft                 | Crime              | Burglary, robbery, larceny                      |
| COL-VEH-COL  | Vehicle Collision     | Auto               | Impact with another vehicle or object           |
| COL-VEH-COMP | Vehicle Comprehensive | Auto               | Weather, theft, vandalism                       |
| COL-LIAB     | Third Party Liability | Liability          | Bodily injury or property damage to third party |
| COL-EQUIP    | Equipment Breakdown   | Equipment          | Mechanical or electrical failure                |
| COL-WIND     | Wind / Storm          | Weather            | Wind, hail, hurricane                           |
| COL-INJURY   | Bodily Injury         | Liability          | Personal injury to claimant                     |
| COL-OTHER    | Other / Unknown       | General            | Catch-all for uncategorised losses              |

# 6 Reserve Management Workflow

## 6.1 Overview

Reserves represent the financial estimate of the expected cost to settle
a claim. They are set at intake (optional) and adjusted throughout the
claim lifecycle. Every reserve change triggers a simulated GL posting
job (Hangfire). Reserves above defined authority thresholds require
approval before the GL posting job is enqueued.

## 6.2 Reserve Components

Each reserve is associated with a component type that classifies the
nature of the cost:

|                        |                                                                  |                      |
|------------------------|------------------------------------------------------------------|----------------------|
| **Component**          | **Description**                                                  | **May Go Negative?** |
| Indemnity              | The estimated indemnity payment to the claimant for their loss   | No                   |
| Expense                | Allocated claim adjustment expenses (investigation, expert fees) | No                   |
| ALAE                   | Allocated Loss Adjustment Expense — attorney and court costs     | No                   |
| SubrogationRecoverable | Expected subrogation or salvage recovery (reduces net cost)      | Yes (by design)      |

A claim may have multiple reserve components simultaneously. For
example, a claim might have an Indemnity reserve of \$25,000, an Expense
reserve of \$5,000, and a SubrogationRecoverable reserve of −\$8,000.

## 6.3 Authority Thresholds

Reserve amounts are validated against authority thresholds at the time
of creation or adjustment. The threshold applies to the amount of the
individual transaction (not the running total).

|                             |                              |                                                                                           |
|-----------------------------|------------------------------|-------------------------------------------------------------------------------------------|
| **Transaction Amount**      | **Authority Level Required** | **System Behaviour**                                                                      |
| ≤ \$10,000                  | Auto-approved (any role)     | Reserve created with status Approved; GL posting job enqueued immediately                 |
| \> \$10,000 and ≤ \$100,000 | Supervisor or Manager        | Reserve created with status PendingApproval; no GL posting until approval granted         |
| \> \$100,000                | Manager only                 | Reserve created with status PendingApproval; no GL posting until approved by Manager role |

|          |                                                                                                                                                                                                   |
|----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **RULE** | A user may not approve their own reserve transactions, even if their role permits the amount. The system must compare the submitter's userId with the approver's userId and reject self-approval. |

## 6.4 Reserve Approval Workflow

When a reserve is submitted above the auto-approval threshold:

5.  The reserve is created with status PendingApproval.

6.  The GL posting job is NOT created at this point.

7.  An approver with the required role (Supervisor or Manager) reviews
    the pending reserve in the Claim Detail / Reserves tab.

8.  On Approve: reserve status → Approved; GL posting job enqueued
    immediately.

9.  On Reject: reserve status → Rejected; rejection reason recorded; the
    submitter may re-submit with a revised amount.

10. A re-submitted reserve creates a new reserve record; the original
    rejected record is retained in history.

|          |                                                                                                                                                                                                                                 |
|----------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **RULE** | A reserve in PendingApproval status may not be modified. If the submitter wishes to change the amount before approval, they must first retract the pending reserve (which sets its status to Cancelled), then submit a new one. |

## 6.5 GL Posting Simulation

When a reserve is approved (either auto-approved or manually approved),
the system enqueues a Hangfire background job that simulates a general
ledger posting. This simulates what in production would be a real
double-entry accounting transaction.

The job must:

- **Use an idempotency key:**
  Reserve:{ReserveId}:Change:{ChangeSequence}. Running the job twice
  with the same key must produce no duplicate log entries.

- **Be re-entrant safe:** if the job fails and is retried, it must not
  create duplicate audit entries.

- **Log a structured entry:** On execution, write a ClaimAuditLog entry
  with EventType = GL_POSTING_SIMULATED, describing the simulated
  journal: DR Change in Outstanding Reserves / CR Outstanding Loss
  Reserves, Amount = {reserveAmount}.

- **Update the reserve:** On successful job execution, set the reserve's
  PostingStatus to Posted.

## 6.6 Reserve History

Every reserve transaction must be stored in a history log. The current
reserve value is the sum of all transactions for that component on the
claim. The history record stores: previous amount, new amount (or
delta), change reason, changed by user, timestamp, approval reference
(if applicable), and GL posting status.

|               |                                                                                                                                                                                                  |
|---------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **IMPORTANT** | Reserves use an event-sourcing pattern: you do not UPDATE a reserve record. You INSERT a new transaction record. The current balance is computed as the sum of all posted/approved transactions. |

# 7 Business Rules

## 7.1 FNOL / Claim Creation Rules

|             |                                                                           |
|-------------|---------------------------------------------------------------------------|
| **BR-C-01** | Loss date must not be in the future. This is a Critical validation issue. |

|             |                                                                                                                                                                                                                                                                                                                                |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-C-02** | Loss date must fall within the linked policy's effective and expiration dates. If it falls outside, a Warning validation issue is generated: 'Loss date is outside the policy effective period.' The claim can be created (it remains in Draft) but this warning must be cleared or acknowledged before transitioning to Open. |

|             |                                                                                                                                                                           |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-C-03** | A claim must have at least one ClaimParty with role Claimant before it can transition from Draft to Open. This is a Critical validation issue that blocks the transition. |

|             |                                                                                                                                                                                    |
|-------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-C-04** | Claim number must be unique per organisation and generated atomically. Format: CLM-{YYYY}-{7-digit-zero-padded-sequence}. Soft-deleted claims still consume their sequence number. |

|             |                                                                                                                                             |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-C-05** | CauseOfLossCode must exist and be active in the reference table. Supplying an inactive or non-existent code is a Critical validation issue. |

|             |                                                                                                                                                                                                                                      |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-C-06** | If no policy is linked (PolicyId is null), the claim is created with a Warning: 'No policy linked — claim requires policy association before financial actions are permitted.' Reserve creation is blocked until a policy is linked. |

|             |                                                                                                       |
|-------------|-------------------------------------------------------------------------------------------------------|
| **BR-C-07** | Loss description is required and must be at least 20 characters. This is a Critical validation issue. |

## 7.2 Reserve Rules

|             |                                                                                                               |
|-------------|---------------------------------------------------------------------------------------------------------------|
| **BR-R-01** | A reserve transaction amount must be greater than zero (except SubrogationRecoverable which may be negative). |

|             |                                                                                                                                                                                                                          |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-R-02** | Authority thresholds: ≤ \$10,000 = auto-approved; \> \$10,000 and ≤ \$100,000 = requires Supervisor or Manager; \> \$100,000 = requires Manager. GL posting job is enqueued only after the required approval is granted. |

|             |                                                                                                                                    |
|-------------|------------------------------------------------------------------------------------------------------------------------------------|
| **BR-R-03** | A user may not approve their own reserve. The system must reject self-approval with a 422 error: 'Self-approval is not permitted.' |

|             |                                                                                                                                                                                  |
|-------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-R-04** | When a reserve is rejected, the submitter may create a new reserve transaction with a revised amount. The original rejected transaction remains in history with status Rejected. |

|             |                                                                                                                                                                                                                                                                                            |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-R-05** | Total approved reserves across all components for a single claim must not exceed \$10,000,000. If a new reserve would cause the total to exceed this limit, the system generates a Warning and requires a Manager to set an override flag on the claim before the reserve can be approved. |

|             |                                                                                                                                                                        |
|-------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-R-06** | GL posting jobs use idempotency key: Reserve:{ReserveId}:Change:{ChangeSequence}. The job is re-entrant safe — executing it twice produces no duplicate audit entries. |

## 7.3 Status Transition Rules

|              |                                                                                                                                                                                               |
|--------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-ST-01** | Status transitions must follow the state machine defined in Section 4.2. Any attempt to transition to an invalid next status returns HTTP 422 with a message listing the valid next statuses. |

|              |                                                                                                                      |
|--------------|----------------------------------------------------------------------------------------------------------------------|
| **BR-ST-02** | Transitioning to Open requires: (a) no Critical validation issues on the claim, and (b) at least one Claimant party. |

|              |                                                                                                                                                                      |
|--------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-ST-03** | Transitioning to Closed requires all conditions in Section 4.3 to be satisfied. If any condition fails, the API returns HTTP 422 with a list of blocking conditions. |

|              |                                                                                                                                                                        |
|--------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-ST-04** | Transitioning to Reopened requires the caller to have Supervisor role and provide a non-empty reopen reason. The claim immediately transitions to Open after Reopened. |

## 7.4 Document Rules

|             |                                                                                                                                                                                             |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-D-01** | Documents are stored in Azure Blob Storage. The storage path is: claim-documents/{organisationId}/{claimId}/{filename}. The filename must be sanitised to remove path traversal characters. |

|             |                                                                                                                      |
|-------------|----------------------------------------------------------------------------------------------------------------------|
| **BR-D-02** | Document retrieval returns a short-lived SAS URL with a 1-hour TTL. Document bytes are not streamed through the API. |

|             |                                                                                                                                                                                                   |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-D-03** | If Azure Blob Storage is not configured, the implementation must fall back to local filesystem storage using a configurable IStorageService interface. Provider is selected via appsettings.json. |

## 7.5 Party Rules

|             |                                                                                                                                                                    |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-P-01** | A claim must have exactly zero or more parties, but must have at least one Claimant before transitioning to Open. This is enforced as a Critical validation issue. |

|             |                                                                                                                                                         |
|-------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-P-02** | Party roles supported: Claimant, Insured, ThirdParty, Witness, Attorney. A claim may have multiple parties of the same role (e.g., multiple claimants). |

## 7.6 Audit Rules

|             |                                                                                                                                                    |
|-------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-A-01** | The ClaimAuditLog is append-only. No UPDATE or DELETE operations are ever performed on audit log records. The application layer must enforce this. |

|             |                                                                                                                                                                                                                                            |
|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **BR-A-02** | Every significant business action on a claim must create an audit log entry: claim created, status changed, party added/removed, reserve created/approved/rejected, document uploaded, GL posting simulated, claim closed, claim reopened. |

# 8 Validation Rules

The following table consolidates all field-level validation rules for
the Claim and Reserve creation commands. These must be implemented as
FluentValidation validators on MediatR Commands.

|                  |                                                          |              |                                                                         |
|------------------|----------------------------------------------------------|--------------|-------------------------------------------------------------------------|
| **Field**        | **Rule**                                                 | **Severity** | **Error Message**                                                       |
| LossDate         | Must not be in the future                                | Critical     | Loss date cannot be in the future.                                      |
| LossDate         | Must be a valid date                                     | Critical     | Loss date is required.                                                  |
| LossDate         | If policy linked: must be within policy effective period | Warning      | Loss date is outside the policy effective period.                       |
| LossDescription  | Required; minimum 20 characters                          | Critical     | Loss description is required and must be at least 20 characters.        |
| CauseOfLossCode  | Must exist and be active in reference data               | Critical     | Cause of loss code is not recognised or is inactive.                    |
| PolicyId         | If null: warning only                                    | Warning      | No policy linked. Policy must be associated before reserves can be set. |
| ClaimParties     | At least one Claimant before Open transition             | Critical     | At least one Claimant party is required to open a claim.                |
| ReserveAmount    | Must be \> 0 (unless SubrogationRecoverable)             | Critical     | Reserve amount must be greater than zero.                               |
| ReserveComponent | Must be one of the defined component types               | Critical     | Invalid reserve component type.                                         |
| ReserveAmount    | Check aggregate: total across all components ≤ \$10M     | Warning      | Total reserves will exceed \$10,000,000. Manager override required.     |
| StatusTransition | Must be a valid next status per state machine            | Critical     | Transition from {from} to {to} is not permitted.                        |
| StatusTransition | Closed: all closure conditions satisfied                 | Critical     | Claim cannot be closed — {condition} is not satisfied.                  |
| ReserveApproval  | Approver role must be Supervisor or Manager              | Critical     | Your role does not have authority to approve this reserve amount.       |
| ReserveApproval  | Approver must not be the submitter                       | Critical     | Self-approval is not permitted.                                         |

# 9 Required Data Entities

The following entities must be designed and implemented. Column
definitions use the conventions from Section 15. You do not need to copy
the schema verbatim — you must design your own schema that satisfies
these functional requirements.

## 9.1 Claims

The central aggregate root. One row per claim.

|                       |                                                                       |
|-----------------------|-----------------------------------------------------------------------|
| **ClaimId**           | Primary key (GUID)                                                    |
| **OrganisationId**    | Tenant isolation (GUID, NOT NULL)                                     |
| **ClaimNumber**       | System-generated unique identifier (NVARCHAR(50), NOT NULL)           |
| **PolicyId**          | FK to simulated policy table (GUID, nullable — unknown policy intake) |
| **PolicyNumber**      | Denormalised for display; populated from linked policy                |
| **ClientName**        | Insured/policyholder name; denormalised for display                   |
| **Status**            | Claim status (NVARCHAR(50), NOT NULL)                                 |
| **Severity**          | Catastrophic / Critical / Standard / Minor (NVARCHAR(50))             |
| **ReportedDate**      | When FNOL was received (DATETIMEOFFSET(7), NOT NULL)                  |
| **AssignedHandlerId** | Assigned handler user ID (GUID, nullable)                             |
| **ClosedAt**          | When claim was closed (DATETIMEOFFSET(7), nullable)                   |
| **ClosureReason**     | Reason code or text for closure (nullable)                            |
| **Notes**             | Internal notes (NVARCHAR(MAX), nullable)                              |
| **IsDeleted**         | Soft delete flag                                                      |
| **CreatedAt**         | Audit timestamp                                                       |
| **UpdatedAt**         | Audit timestamp (nullable)                                            |
| **UserCreated**       | Audit user ID (nullable)                                              |
| **UserModified**      | Audit user ID (nullable)                                              |
| **RowVer**            | Optimistic concurrency (ROWVERSION)                                   |

## 9.2 LossEvents

The loss incident linked to the claim.

|                         |                                                                |
|-------------------------|----------------------------------------------------------------|
| **LossEventId**         | Primary key                                                    |
| **ClaimId**             | FK to Claims                                                   |
| **LossDate**            | Date/time of the loss event (DATETIMEOFFSET(7), NOT NULL)      |
| **LossDescription**     | Narrative description (NVARCHAR(MAX), NOT NULL)                |
| **LossLocation**        | Free-text location description (NVARCHAR(500), nullable)       |
| **CauseOfLossCode**     | FK to CauseOfLossCodes.Code (NVARCHAR(50), NOT NULL)           |
| **EstimatedLossAmount** | Initial dollar estimate (DECIMAL(19,4), nullable)              |
| **ReportDate**          | Date claim was formally reported (DATETIMEOFFSET(7), NOT NULL) |
| **PoliceReportNumber**  | If applicable (NVARCHAR(100), nullable)                        |

## 9.3 ClaimParties

All people and organisations involved in the claim.

|                  |                                                                     |
|------------------|---------------------------------------------------------------------|
| **ClaimPartyId** | Primary key                                                         |
| **ClaimId**      | FK to Claims                                                        |
| **PartyRole**    | Claimant / Insured / ThirdParty / Witness / Attorney (NVARCHAR(50)) |
| **PartyType**    | Person / Company (NVARCHAR(20))                                     |
| **FirstName**    | For person parties (NVARCHAR(100), nullable)                        |
| **LastName**     | For person parties (NVARCHAR(100), nullable)                        |
| **CompanyName**  | For company parties (NVARCHAR(255), nullable)                       |
| **Email**        | Contact email (NVARCHAR(255), nullable)                             |
| **Phone**        | Contact phone (NVARCHAR(50), nullable)                              |
| **Notes**        | Additional notes (NVARCHAR(MAX), nullable)                          |
| **IsActive**     | Soft remove from claim (BIT)                                        |

## 9.4 ClaimRiskObjects

Assets or property linked to the claim as affected by the loss.

|                       |                                                                                 |
|-----------------------|---------------------------------------------------------------------------------|
| **ClaimRiskObjectId** | Primary key                                                                     |
| **ClaimId**           | FK to Claims                                                                    |
| **AssetType**         | Vehicle / Property / Person / Equipment / Other (NVARCHAR(50))                  |
| **AssetDescription**  | Free-text description of the asset (NVARCHAR(500))                              |
| **DamageDescription** | Description of damage sustained (NVARCHAR(MAX), nullable)                       |
| **IsPrimary**         | Is this the primary risk object? (BIT)                                          |
| **AssetReference**    | Optional reference number, VIN, property address etc. (NVARCHAR(255), nullable) |

## 9.5 ClaimReserveComponents

Each row represents a reserve component on a claim. The current value is
computed from the history table.

|                        |                                                                           |
|------------------------|---------------------------------------------------------------------------|
| **ReserveComponentId** | Primary key                                                               |
| **ClaimId**            | FK to Claims                                                              |
| **Component**          | Indemnity / Expense / ALAE / SubrogationRecoverable (NVARCHAR(50))        |
| **CurrentAmount**      | Computed: sum of all posted/approved history transactions (DECIMAL(19,4)) |
| **Status**             | Active / Closed (NVARCHAR(50))                                            |
| **Notes**              | Optional notes (NVARCHAR(MAX), nullable)                                  |

## 9.6 ReserveHistory

Append-only event log of all reserve transactions. The current reserve
balance is the sum of all transactions for a component.

|                        |                                                                                 |
|------------------------|---------------------------------------------------------------------------------|
| **ReserveHistoryId**   | Primary key                                                                     |
| **ReserveComponentId** | FK to ClaimReserveComponents                                                    |
| **ClaimId**            | FK to Claims (denormalised for query convenience)                               |
| **TransactionType**    | Add / Adjust / Reverse (NVARCHAR(50))                                           |
| **Amount**             | Delta amount (positive = increase, negative = decrease) (DECIMAL(19,4))         |
| **PreviousBalance**    | Balance before this transaction (DECIMAL(19,4))                                 |
| **NewBalance**         | Balance after this transaction (DECIMAL(19,4))                                  |
| **ApprovalStatus**     | AutoApproved / PendingApproval / Approved / Rejected / Cancelled (NVARCHAR(50)) |
| **ApprovedByUserId**   | User who approved (nullable)                                                    |
| **ApprovedAt**         | Timestamp of approval (DATETIMEOFFSET(7), nullable)                             |
| **RejectedByUserId**   | User who rejected (nullable)                                                    |
| **RejectedAt**         | Timestamp of rejection (nullable)                                               |
| **RejectionReason**    | Free text (nullable)                                                            |
| **ChangeReason**       | Why the reserve was changed (NVARCHAR(500))                                     |
| **PostingStatus**      | Pending / Posted / Failed / Cancelled (NVARCHAR(50))                            |
| **PostingJobId**       | Hangfire job ID (nullable)                                                      |
| **IdempotencyKey**     | Reserve:{ReserveId}:Change:{Seq} (NVARCHAR(200))                                |
| **ChangeSequence**     | Monotonically increasing per component (INT)                                    |
| **SubmittedByUserId**  | User who submitted (nullable)                                                   |
| **CreatedAt**          | Timestamp                                                                       |

## 9.7 ClaimDocuments

Metadata for documents attached to a claim. Binary content is stored in
Azure Blob Storage.

|                      |                                                                  |
|----------------------|------------------------------------------------------------------|
| **ClaimDocumentId**  | Primary key                                                      |
| **ClaimId**          | FK to Claims                                                     |
| **DocumentType**     | E.g. PoliceReport, MedicalReport, Invoice, Other (NVARCHAR(100)) |
| **DocumentName**     | Filename as uploaded (NVARCHAR(255))                             |
| **BlobPath**         | Full path in blob storage (NVARCHAR(500))                        |
| **ContentType**      | MIME type (NVARCHAR(100))                                        |
| **FileSizeBytes**    | File size in bytes (BIGINT)                                      |
| **UploadedAt**       | Upload timestamp (DATETIMEOFFSET(7))                             |
| **UploadedByUserId** | User who uploaded (GUID, nullable)                               |
| **Notes**            | Optional description (NVARCHAR(500), nullable)                   |

## 9.8 ClaimAuditLog

Immutable append-only event log. No updates or deletes permitted.

|                       |                                                                                                                                                                                    |
|-----------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **AuditLogId**        | Primary key                                                                                                                                                                        |
| **ClaimId**           | FK to Claims                                                                                                                                                                       |
| **EventType**         | E.g. CLAIM_CREATED, STATUS_CHANGED, PARTY_ADDED, RESERVE_CREATED, RESERVE_APPROVED, RESERVE_REJECTED, DOCUMENT_UPLOADED, GL_POSTING_SIMULATED, SLA_BREACH_DETECTED (NVARCHAR(100)) |
| **Description**       | Human-readable description of the event (NVARCHAR(MAX))                                                                                                                            |
| **OldValue**          | Previous value as JSON (nullable, NVARCHAR(MAX))                                                                                                                                   |
| **NewValue**          | New value as JSON (nullable, NVARCHAR(MAX))                                                                                                                                        |
| **RelatedEntityId**   | ID of the related entity (reserve, document, etc.) (GUID, nullable)                                                                                                                |
| **RelatedEntityType** | Type name of the related entity (nullable)                                                                                                                                         |
| **CorrelationId**     | Request correlation ID (GUID, nullable)                                                                                                                                            |
| **CreatedAt**         | Timestamp (DATETIMEOFFSET(7), NOT NULL)                                                                                                                                            |
| **CreatedByUserId**   | User or system actor (GUID, nullable)                                                                                                                                              |

## 9.9 CauseOfLossCodes

Reference/lookup table for cause of loss codes. Seeded at migration
time.

|                       |                                                                     |
|-----------------------|---------------------------------------------------------------------|
| **CauseOfLossCodeId** | Primary key                                                         |
| **Code**              | Unique code (NVARCHAR(50), UNIQUE)                                  |
| **Name**              | Display name (NVARCHAR(255))                                        |
| **PerilCategory**     | Property / Auto / Liability / Weather / Equipment / Crime / General |
| **IsActive**          | Whether available for selection (BIT)                               |
| **SortOrder**         | Display order (INT)                                                 |

## 9.10 Policies (Simulated)

Minimal policy table seeded with the data from Section 5.5. This
represents a real policy table in a production system; here it is a
simplified simulation.

|                    |                                                           |
|--------------------|-----------------------------------------------------------|
| **PolicyId**       | Primary key                                               |
| **PolicyNumber**   | Unique policy number (NVARCHAR(50))                       |
| **ClientName**     | Policyholder name (NVARCHAR(255))                         |
| **EffectiveDate**  | Policy start date (DATE)                                  |
| **ExpirationDate** | Policy end date (DATE)                                    |
| **Status**         | Active / Expired / Cancelled (NVARCHAR(50))               |
| **CoverageTypes**  | Comma-separated list or JSON array of coverage type names |

# 10 API Behaviour

All endpoints are RESTful JSON APIs. Validation errors return HTTP 422
Unprocessable Entity with a structured error body. Authentication is
Bearer token (JWT). All write operations are idempotent where an
Idempotency-Key header is provided.

## 10.1 Claims Endpoints

|                                           |            |                                                                                                                                                                                                                                                                              |
|-------------------------------------------|------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                              | **Method** | **Behaviour**                                                                                                                                                                                                                                                                |
| POST /api/claims                          | POST       | Create a new claim (FNOL). Validates all business rules. Generates ClaimNumber atomically. Creates LossEvent, Parties, RiskObjects, and optional initial reserve in a single transaction. Returns 201 with the created claim including ClaimNumber.                          |
| GET /api/claims                           | GET        | Paginated, filterable list of claims. Query params: status, dateFrom, dateTo, assignedHandlerId, causeOfLossCode, policyId, search (partial claim number or client name). Returns summary rows (id, claimNumber, clientName, policyNumber, lossDate, status, totalReserves). |
| GET /api/claims/{id}                      | GET        | Full claim detail including parties, risk objects, reserve summary, document list, and recent audit log entries.                                                                                                                                                             |
| PUT /api/claims/{id}/status               | PUT        | Transition claim status. Body: { targetStatus, reason? }. Validates against state machine. Returns 422 with blocking conditions if transition is not permitted.                                                                                                              |
| GET /api/claims/{id}/audit                | GET        | Paginated audit log for the claim. Returns append-only event list in reverse-chronological order.                                                                                                                                                                            |
| POST /api/claims/{id}/parties             | POST       | Add a party to the claim.                                                                                                                                                                                                                                                    |
| DELETE /api/claims/{id}/parties/{partyId} | DELETE     | Soft-remove a party (sets IsActive = false). Returns 422 if removing the last Claimant.                                                                                                                                                                                      |
| POST /api/claims/{id}/documents           | POST       | Upload a document to the claim. Multipart form. Stores in Azure Blob Storage. Creates ClaimDocuments record.                                                                                                                                                                 |
| GET /api/claims/{id}/documents            | GET        | List documents for the claim. Each document includes a short-lived SAS download URL (1-hour TTL).                                                                                                                                                                            |

## 10.2 Reserve Endpoints

|                                                |            |                                                                                                                                                                                                                                                                  |
|------------------------------------------------|------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                                   | **Method** | **Behaviour**                                                                                                                                                                                                                                                    |
| POST /api/claims/{id}/reserves                 | POST       | Open or adjust a reserve component. Body: { component, amount, changeReason, transactionType }. Validates authority. Creates ReserveHistory record. If auto-approved, enqueues GL posting job. Returns 201 with the created transaction and its approval status. |
| GET /api/claims/{id}/reserves                  | GET        | Returns reserve summary (current balance per component) and full transaction history for the claim.                                                                                                                                                              |
| POST /api/claims/{id}/reserves/{txnId}/approve | POST       | Approve a pending reserve. Requires Supervisor or Manager role. Validates role, validates not self-approving. On success: updates approval status, enqueues GL posting job.                                                                                      |
| POST /api/claims/{id}/reserves/{txnId}/reject  | POST       | Reject a pending reserve. Requires Supervisor or Manager role. Body: { rejectionReason }. Sets transaction status to Rejected.                                                                                                                                   |
| POST /api/claims/{id}/reserves/{txnId}/retract | POST       | Submitter retracts a pending reserve before approval. Sets status to Cancelled.                                                                                                                                                                                  |

## 10.3 Reference Data Endpoints

|                                        |            |                                                                                                                                                                      |
|----------------------------------------|------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                           | **Method** | **Behaviour**                                                                                                                                                        |
| GET /api/reference/cause-of-loss-codes | GET        | List all active cause of loss codes. Optional filter: ?perilCategory={category}.                                                                                     |
| GET /api/reference/claim-statuses      | GET        | List all claim status values with their valid next-status transitions.                                                                                               |
| GET /api/policies/search               | GET        | Search simulated policies. Query params: q (policy number or client name). Returns matching policies including effectiveDate, expirationDate, status, coverageTypes. |

## 10.4 Error Response Structure

All error responses must follow a consistent JSON structure:

{ "type": "ValidationError", "title": "One or more validation errors
occurred.", "status": 422, "errors": { "LossDate": \["Loss date cannot
be in the future."\], "ClaimParties": \["At least one Claimant party is
required."\] } }

# 11 Frontend Behaviour

The Angular frontend consists of three primary screens. All API
communication must go through a typed Angular service layer — no direct
HTTP calls in components.

## 11.1 Claims List (Dashboard)

The entry point of the application. Provides a paginated, filterable
overview of all claims.

### Required Elements

- Paginated table with columns: Claim Number, Policy Number, Client
  Name, Loss Date, Cause of Loss, Status (badge), Total Reserve Amount

- **Status badges must be colour-coded:** Draft = grey \| Open = blue \|
  UnderInvestigation = orange \| PendingPayment = purple \| Closed =
  green \| Reopened = amber \| Withdrawn = dark grey

- Filter bar with controls for: Status (multi-select), Date range (loss
  date from/to), assigned handler (text search), cause of loss
  (dropdown)

- Each row is clickable and navigates to the Claim Detail screen

- **Log New Claim** button (primary action) opens the FNOL intake form

- Show total claim count and page size selector

- Empty state: friendly message when no claims match the current filter

## 11.2 FNOL Intake Form

A multi-step reactive form for creating a new claim. Each step validates
independently before the user can advance.

### Step 1 — Policy & Loss Details

- Policy search: typeahead input field; as the user types a policy
  number or client name, a dropdown shows matching policies from the
  API; selecting a policy populates policy number, client name, and
  effective dates

- **In-force indicator:** a coloured badge next to the selected policy —
  green if the entered loss date falls within the policy effective
  period, amber if the policy is expired or the loss date is outside the
  window

- If the user cannot find a policy, an 'Unknown Policy' toggle allows
  proceeding without a policy link

- Loss Date: date-time picker; validation error if in the future

- Cause of Loss Code: searchable dropdown populated from
  /api/reference/cause-of-loss-codes

- Loss Description: textarea with minimum character count indicator (20
  chars minimum)

- Loss Location: free text field

- Estimated Loss Amount: optional numeric input

### Step 2 — Parties & Risk Objects

- **Parties section:** Add Party button opens an inline form row. Party
  role (dropdown), party type (Person/Company), name fields, email,
  phone. At least one Claimant required — show inline warning if none
  added.

- **Risk Objects section:** Add Risk Object button opens an inline form
  row. Asset type (dropdown), asset description, damage description,
  reference number (optional). At least one risk object is recommended;
  show advisory message if none added.

- Both sections show added items as styled chips/cards with a remove
  button

### Step 3 — Initial Reserve & Review

- Optional Initial Reserve section: Component dropdown (Indemnity,
  Expense, ALAE, SubrogationRecoverable) + Amount field

- **Authority threshold indicator:** as the user types an amount, show a
  real-time indicator: '✓ Auto-approved (≤ \$10,000)', '⚠ Supervisor
  approval required', '⚠ Manager approval required'

- Review summary: a read-only table showing all entered data before
  submission

- **Create Claim button:** disabled until Step 1 and Step 2 are valid;
  confirmation dialog if any Warning validation issues exist

- On success: navigate to Claim Detail with a success snackbar showing
  the generated Claim Number

- On error: display validation errors inline and at the top of the
  relevant step

## 11.3 Claim Detail Screen

Full claim record view with five tabbed sections.

### Header (Always Visible)

- Claim Number (prominent, copyable)

- Status chip (colour-coded, as per dashboard)

- Policy Number and Client Name

- Loss Date and Cause of Loss

- **Transition button:** dropdown or button showing valid next statuses
  for the current status; opens confirmation dialog; for Closed
  transition shows pre-flight checklist

- Assigned Handler display

### Tab 1 — Overview

- Loss event details (date, description, location, cause)

- Severity badge

- Estimated loss amount

- Claim notes field (editable)

### Tab 2 — Parties

- List of all parties: role badge, name, contact details,
  active/inactive status

- Add Party button opens inline form

- Remove button per party (disabled for last Claimant)

### Tab 3 — Reserves

- Summary cards at top: one card per component showing Current Balance
  and Pending Amount (amber if pending approval)

- Transaction history table: Date \| Type \| Component \| Amount (green
  for increase, red for decrease) \| Status badge \| Submitted By \|
  Approved By

- **Add Reserve button** opens a slide-in panel with Component, Amount,
  Reason fields and real-time authority indicator

- **Approve / Reject buttons** appear on each row with status
  PendingApproval; only visible to Supervisor or Manager role

- **Retract button** appears for the submitter on their own pending
  transactions

- GL Posting Status badge per row: Pending \| Posted \| Failed (with
  retry button for Failed)

### Tab 4 — Documents

- List of uploaded documents: name, type, upload date, uploader, file
  size

- Download button: calls API to get SAS URL and opens in new tab

- **Upload button** opens file picker; multipart upload to
  /api/claims/{id}/documents; shows progress indicator

### Tab 5 — Audit Log

- Reverse-chronological list of all audit events

- Each entry: timestamp, event type badge, description, user, related
  entity link (if applicable)

- Paginated; no editing or deletion possible from this tab

## 11.4 General Frontend Requirements

- Angular routing with lazy-loaded feature modules (ClaimsList,
  ClaimDetail, FnolIntake)

- **Mock authentication service:** hardcoded users for handler,
  supervisor, and manager roles. HTTP interceptor adds Bearer token to
  all requests. UI shows logged-in user name and role in header. Role
  switcher for testing (not required in production).

- Angular Material theming: consistent professional colour scheme with a
  custom primary palette

- **HTTP error handling:** all API errors are caught at the service
  layer and displayed as Angular Material snackbar notifications with
  appropriate severity

- **Loading states:** all async operations show a loading spinner or
  skeleton loader; buttons are disabled during pending API calls

- Reactive Forms throughout; all form validation messages follow Angular
  Material error pattern

- Responsive layout: usable at 1280px+ minimum width

# 12 Background Processing

Two Hangfire background jobs are required. Both must be registered on
application startup.

## 12.1 GL Posting Simulation Job

This job runs when a reserve is approved (auto or manual). It simulates
posting the reserve change to a general ledger.

|                      |                                                                                                                                                                                                                                                                      |
|----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Job Name**         | PostGLReserveChangeJob                                                                                                                                                                                                                                               |
| **Trigger**          | Enqueued immediately on reserve approval (fire-and-forget, enqueued via Hangfire BackgroundJob.Enqueue)                                                                                                                                                              |
| **Input**            | ReserveHistoryId, ClaimId, IdempotencyKey                                                                                                                                                                                                                            |
| **Idempotency Key**  | Reserve:{ReserveId}:Change:{ChangeSequence}                                                                                                                                                                                                                          |
| **Behaviour**        | Check if IdempotencyKey already has a Posted status in ReserveHistory. If yes, no-op. If no: write ClaimAuditLog entry with EventType = GL_POSTING_SIMULATED; update ReserveHistory.PostingStatus = Posted; update ReserveHistory.PostingJobId with Hangfire job ID. |
| **Failure handling** | On exception: Hangfire retries automatically (default retry policy). After all retries exhausted: set PostingStatus = Failed, write audit log entry GL_POSTING_FAILED.                                                                                               |
| **Re-entrancy**      | The job must be safe to run multiple times. Check idempotency key before any write operation.                                                                                                                                                                        |

## 12.2 SLA Monitoring Job

A recurring job that scans open claims for SLA breaches.

|                 |                                                                                                                                                                                                                                                  |
|-----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Job Name**    | SlaMonitoringJob                                                                                                                                                                                                                                 |
| **Schedule**    | Every 15 minutes (Hangfire recurring job: '\*/15 \* \* \* \*')                                                                                                                                                                                   |
| **Trigger**     | Scheduled; no manual trigger required                                                                                                                                                                                                            |
| **Behaviour**   | Find all claims with Status IN (Draft, Open) WHERE UpdatedAt \< (now − 48 hours). For each: write ClaimAuditLog entry with EventType = SLA_BREACH_DETECTED and description 'Claim has not been updated in 48 hours'. Do not change claim status. |
| **Idempotency** | The same claim should not produce duplicate breach events. Track last breach event per claim; only add a new entry if 24+ hours have passed since the last SLA_BREACH_DETECTED entry.                                                            |
| **Notes**       | This job does not send emails or push notifications; it only writes audit log entries. Future notification integration can read from the audit log.                                                                                              |

# 13 Document Management

Documents are binary files uploaded by users and stored externally to
the database.

|                      |                                                                                                                                                                                                                                   |
|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Storage Provider** | Azure Blob Storage (primary); local filesystem (fallback)                                                                                                                                                                         |
| **Blob Container**   | claim-documents                                                                                                                                                                                                                   |
| **Blob Path**        | {organisationId}/{claimId}/{sanitisedFilename}                                                                                                                                                                                    |
| **Max File Size**    | No hard limit specified in this assessment; implement a reasonable 50 MB limit                                                                                                                                                    |
| **Retrieval**        | API generates a short-lived SAS URL (1-hour TTL) using Azure SDK. Frontend opens URL in new tab. Document bytes must NOT be proxied through the API.                                                                              |
| **Fallback**         | When Azure Blob Storage is not configured (local dev), store files in the local filesystem under /uploads/{organisationId}/{claimId}/. Return a local URL for download.                                                           |
| **Interface**        | Storage provider must be behind an IStorageService interface with AzureBlobStorageService and LocalFileSystemStorageService implementations. Provider selected in appsettings.json: StorageProvider: AzureBlob \| LocalFileSystem |
| **Audit**            | Every document upload creates a ClaimAuditLog entry: EventType = DOCUMENT_UPLOADED, RelatedEntityId = documentId.                                                                                                                 |
| **Validation**       | Reject files with MIME types outside an allowlist: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.                                                                                                                                          |

# 14 Audit & Logging

The ClaimAuditLog table is an immutable append-only event log. It is the
single source of truth for all business actions taken on a claim.

## 14.1 Events That Must Be Logged

|                        |                                                                                          |
|------------------------|------------------------------------------------------------------------------------------|
| **EventType**          | **Trigger**                                                                              |
| CLAIM_CREATED          | New claim successfully created via POST /api/claims                                      |
| STATUS_CHANGED         | Claim status transition applied; OldValue = previous status, NewValue = new status       |
| PARTY_ADDED            | New ClaimParty added to the claim                                                        |
| PARTY_REMOVED          | ClaimParty soft-removed (IsActive set to false)                                          |
| RESERVE_CREATED        | New reserve transaction submitted                                                        |
| RESERVE_AUTO_APPROVED  | Reserve auto-approved (amount ≤ \$10,000)                                                |
| RESERVE_APPROVED       | Reserve manually approved by supervisor/manager                                          |
| RESERVE_REJECTED       | Reserve rejected by supervisor/manager; OldValue includes rejection reason               |
| RESERVE_RETRACTED      | Submitter retracted their pending reserve                                                |
| GL_POSTING_SIMULATED   | Hangfire GL job executed successfully; NewValue includes simulated journal entry details |
| GL_POSTING_FAILED      | Hangfire GL job failed after all retries; NewValue includes failure reason               |
| DOCUMENT_UPLOADED      | Document uploaded to claim; RelatedEntityId = documentId                                 |
| CLAIM_CLOSED           | Claim transitioned to Closed status; NewValue includes closure reason                    |
| CLAIM_REOPENED         | Claim transitioned from Closed to Reopened; NewValue includes reopen reason              |
| SLA_BREACH_DETECTED    | SLA monitoring job detected a claim not updated within 48 hours                          |
| VALIDATION_ISSUE_ADDED | A new validation issue was recorded against the claim                                    |

## 14.2 Audit Log Integrity Rules

- The ClaimAuditLog table must have no UPDATE or DELETE permissions
  granted in the application.

- All writes to ClaimAuditLog must go through a dedicated
  IAuditLogService; no direct DbContext writes from handlers.

- All timestamps use DATETIMEOFFSET(7); store in UTC.

- CorrelationId from the HTTP request context must be propagated to all
  audit entries within the same request.

# 15 Data Conventions & Constraints

All database tables and EF Core entity configurations must follow these
conventions consistently.

## 15.1 Column Conventions

|                            |                                                                                                                                                                        |
|----------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Primary Keys**           | UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(). Use sequential GUIDs to avoid index fragmentation.                                                                |
| **Monetary Amounts**       | DECIMAL(19,4). Never use money or float types for financial values.                                                                                                    |
| **Timestamps**             | DATETIMEOFFSET(7). All timestamps are stored in UTC with timezone offset information.                                                                                  |
| **Short Codes**            | NVARCHAR(50). For status codes, type codes, cause codes, etc.                                                                                                          |
| **Names**                  | NVARCHAR(255). For display names, client names, file names.                                                                                                            |
| **Free Text**              | NVARCHAR(MAX). For descriptions, narratives, notes, JSON blobs.                                                                                                        |
| **Boolean Flags**          | BIT NOT NULL DEFAULT 0.                                                                                                                                                |
| **Soft Delete**            | IsDeleted BIT NOT NULL DEFAULT 0 + DeletedAt DATETIMEOFFSET(7) NULL. Applied to all master and transaction tables.                                                     |
| **Audit Columns**          | CreatedAt DATETIMEOFFSET(7) NOT NULL, UpdatedAt DATETIMEOFFSET(7) NULL, UserCreated UNIQUEIDENTIFIER NULL, UserModified UNIQUEIDENTIFIER NULL. Required on all tables. |
| **Tenant Isolation**       | OrganisationId UNIQUEIDENTIFIER NOT NULL on every business table. Seed your implementation with a single fixed OrganisationId GUID.                                    |
| **Optimistic Concurrency** | RowVer ROWVERSION NOT NULL on aggregate roots (Claims, ClaimReserveComponents).                                                                                        |

## 15.2 EF Core Configuration

- Use EF Core Fluent API configuration (IEntityTypeConfiguration\<T\>
  classes) — do not rely on data annotations for schema definition

- Apply a global query filter for soft delete:
  modelBuilder.Entity\<T\>().HasQueryFilter(e =\> !e.IsDeleted)

- Configure all decimal properties with precision: HasPrecision(19, 4)

- Configure ROWVERSION as IsRowVersion() for optimistic concurrency

- Use NEWSEQUENTIALID() as the default value expression for all GUID
  primary keys

## 15.3 Naming Conventions

|                      |                                                                          |
|----------------------|--------------------------------------------------------------------------|
| **Database tables**  | PascalCase singular noun (Claims, LossEvents, ClaimParties)              |
| **Database columns** | PascalCase (ClaimId, LossDate, CauseOfLossCode)                          |
| **C# entities**      | PascalCase; match table name                                             |
| **C# DTOs**          | SuffixedWithDto or Request/Response (CreateClaimCommand, ClaimDetailDto) |
| **API routes**       | kebab-case plural nouns (/api/claims, /api/cause-of-loss-codes)          |
| **MediatR commands** | VerbNounCommand (CreateClaimCommand, ApproveReserveCommand)              |
| **MediatR queries**  | GetNounQuery or ListNounsQuery (GetClaimDetailQuery, ListClaimsQuery)    |

## 15.4 Constraints

- All seed data (cause of loss codes, policy records, reference
  statuses) must be applied via EF Core data seeding (HasData) or
  migration-time insert scripts — not at application startup code

- Database migrations must be reproducible from scratch by running
  dotnet ef database update on a fresh SQL Server instance

- The application must start successfully without any manual database
  setup beyond running migrations

- No hard-coded GUIDs in application logic (use configuration or seeded
  reference data IDs)

*End of Specification · Version 1.0 · May 2026*
