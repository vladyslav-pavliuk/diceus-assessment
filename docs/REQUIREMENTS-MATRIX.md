# Requirements matrix — definition of done

One row per requirement. **Source**: `FRS §x` = `docs/spec/claims-frs.md`, `Brief §x` = `docs/spec/assessment-brief.md`,
`D-xx` = `docs/DECISIONS.md` (all ACCEPTED 2026-09-27).

**Enforced in** uses these layers: Domain (entity/aggregate invariant) · Validator (FluentValidation in the MediatR pipeline) ·
Handler (orchestration, lookups) · DB (constraint/index/trigger) · Infra (jobs, storage, auth) · API (endpoint, policy, middleware) · UI.

**Planned test** is named after the rule ID. Project prefixes: `Dom` = ClaimsModule.Domain.Tests, `App` = ClaimsModule.Application.Tests,
`Int` = ClaimsModule.IntegrationTests (WebApplicationFactory + Testcontainers MsSql), `Web` = claims-ui unit tests (Karma/Jest), `Manual` = checklist / smoke script.

**Status**: all rows are `Planned` at Phase 0. In Phase 8 each row becomes `Done` / `Partial` / `Missing`, with evidence (file:line + test).

---

## 1. Brief → FRS rule-ID mapping (D-02)

| Brief ID (§3.4) | Brief meaning | Canonical ID | Note |
|---|---|---|---|
| BR-C-01 | Loss date not in future | BR-C-01 | same |
| BR-C-02 | Out-of-period → warning, recorded in audit | BR-C-02 | FRS adds "acknowledge before Open" (D-19) |
| BR-C-03 | ≥1 Claimant | BR-C-03 | FRS: before Open (D-06) |
| BR-C-04 | Unique claim number, atomic | BR-C-04 | FRS adds "no gaps" (D-10) |
| BR-C-05 | Code exists & active for org | BR-C-05 | tenant-scoped codes (D-12) |
| BR-C-06 | Draft ↛ Closed; required path | **B-C-06** | brief-only (D-20) |
| BR-R-01 | Amount > 0 | BR-R-01 | FRS: Subrogation exception (D-05) |
| BR-R-02 | ≤ $10k auto-approved + GL | BR-R-02 | |
| BR-R-03 | $10k–$100k supervisor | BR-R-02 | |
| BR-R-04 | > $100k manager | BR-R-02 | |
| BR-R-05 | GL idempotency key | BR-R-06 | |
| BR-R-06 | Rejected → resubmit new record | BR-R-04 | |
| BR-R-07 | $10M aggregate + override | BR-R-05 | |
| — | (no self-approval rule in brief) | BR-R-03 | FRS-only |
| — | FRS §4.2 "BR-CL-01" (undefined in FRS) | CC-01..04 | dangling reference (D-02) |

---

## 2. FRS business rules (FRS §7) and closure conditions (FRS §4.3)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| BR-C-01 | Loss date not in the future (Critical → 422 on POST) | FRS §7.1, §8; Brief §3.4.1; D-06, D-32 | Validator (TimeProvider), Domain (LossEvent ctor guard), UI | `App: BR_C_01_Future_loss_date_is_rejected`; `App: BR_C_01_Loss_date_equal_to_now_is_accepted`; `Int: BR_C_01_Post_claim_with_future_loss_date_returns_422` | Planned |
| BR-C-02 | Loss date outside the policy period → Warning issue; must be acknowledged/cleared before Open | FRS §7.1, §8, §5.4; Brief §3.4.1; D-19, D-32 | Domain (issue evaluation, Open guard), Handler (policy lookup), UI (in-force badge) | `Dom: BR_C_02_Loss_date_outside_policy_period_raises_warning`; `Dom: BR_C_02_Boundary_dates_are_inclusive`; `Dom: BR_C_02_Unacknowledged_warning_blocks_open`; `Dom: BR_C_02_Acknowledged_warning_allows_open`; `Int: BR_C_02_Expired_policy_POL_2023_000099_creates_draft_with_warning` | Planned |
| BR-C-03 | ≥1 active Claimant before Draft → Open (Critical issue) | FRS §7.1, §4.2, §8; Brief §3.4.1; D-06 | Domain | `Dom: BR_C_03_Claim_without_claimant_is_created_with_critical_issue`; `Dom: BR_C_03_Open_without_active_claimant_is_rejected`; `Dom: BR_C_03_Adding_claimant_resolves_issue` | Planned |
| BR-C-04 | Claim number unique per org, atomic, gap-free, `CLM-{YYYY}-{0000000}`; soft-deleted claims keep their numbers | FRS §7.1, §5.3; Brief §3.4.1; D-10 | Persistence (counter table in txn), DB (unique `(OrganisationId, ClaimNumber)`), Domain (ClaimNumber VO format) | `Dom: BR_C_04_Claim_number_format_is_CLM_year_7_digits`; `Int: BR_C_04_50_parallel_creates_yield_unique_gap_free_numbers`; `Int: BR_C_04_Rolled_back_create_does_not_consume_number`; `Int: BR_C_04_Counter_resets_per_year` | Planned |
| BR-C-05 | CauseOfLossCode exists and is active for the org (Critical → 422) | FRS §7.1, §8; Brief §3.4.1; D-06, D-12 | Validator (async ref-data lookup), DB (FK) | `App: BR_C_05_Unknown_cause_code_is_rejected`; `App: BR_C_05_Inactive_cause_code_is_rejected` | Planned |
| BR-C-06 | No policy → Warning "No policy linked…"; reserve creation blocked until linked | FRS §7.1, §8, §5.2; D-06 | Domain (reserve guard), Validator (initial reserve + null policy → 422) | `Dom: BR_C_06_Claim_without_policy_gets_warning`; `Dom: BR_C_06_Reserve_on_claim_without_policy_is_rejected`; `App: BR_C_06_Initial_reserve_without_policy_is_rejected`; `Dom: BR_C_06_Linking_policy_resolves_warning_and_unblocks_reserves` | Planned |
| BR-C-07 | Loss description required, ≥ 20 chars (Critical → 422) | FRS §7.1, §8 | Validator, Domain guard, UI (counter) | `App: BR_C_07_Description_shorter_than_20_chars_is_rejected`; `App: BR_C_07_Description_of_exactly_20_chars_is_accepted` | Planned |
| B-C-06 | Draft ↛ Closed (brief path rule) | Brief §3.4.1; D-20 | Domain (transition table) | `Dom: B_C_06_Draft_to_closed_is_rejected` | Planned |
| BR-R-01 | Amount > 0, except SubrogationRecoverable; Adjust = signed non-zero delta; non-Subrogation balance ≥ 0 | FRS §7.2, §8, §6.2, §9.6; Brief §3.4.2; D-05 | Validator (shape), Domain (balance rule) | `App: BR_R_01_Zero_amount_is_rejected`; `App: BR_R_01_Negative_add_on_indemnity_is_rejected`; `Dom: BR_R_01_Negative_subrogation_is_accepted`; `Dom: BR_R_01_Adjust_below_zero_balance_is_rejected`; `Dom: BR_R_01_Reverse_zeroes_component` | Planned |
| BR-R-02 | Authority tiers on \|txn amount\|: ≤10k auto; >10k–≤100k supervisor/manager; >100k manager; GL only after approval | FRS §7.2, §6.3; Brief §3.4.2 (BR-R-02/03/04), A.3; D-05 | Domain (`ReserveAuthorityPolicy`) | `Dom: BR_R_02_Amount_10000_is_auto_approved`; `Dom: BR_R_02_Amount_10000_01_requires_supervisor`; `Dom: BR_R_02_Amount_100000_requires_supervisor`; `Dom: BR_R_02_Amount_100000_01_requires_manager`; `Dom: BR_R_02_Negative_amount_uses_absolute_value`; `Dom: BR_R_02_Supervisor_cannot_approve_above_100000`; `Int: BR_R_02_Pending_reserve_enqueues_no_gl_job` | Planned |
| BR-R-03 | No self-approval → 422 "Self-approval is not permitted." | FRS §7.2, §6.3, §8; D-25 | Domain | `Dom: BR_R_03_Self_approval_is_rejected`; `Int: BR_R_03_Self_approval_returns_422_with_exact_message` | Planned |
| BR-R-04 | Rejected txn stays in history (Rejected); resubmission creates a new record | FRS §7.2, §6.4; Brief BR-R-06 | Domain | `Dom: BR_R_04_Rejected_txn_is_retained_and_resubmission_creates_new_txn` | Planned |
| BR-R-05 | Approved total ≤ $10M unless the Manager override is set; Warning at submit, blocked at approval | FRS §7.2, §8, §3; Brief BR-R-07, A.3; D-11 | Domain, API (Manager policy on override) | `Dom: BR_R_05_Crossing_10M_without_override_warns_and_escalates_to_manager`; `Dom: BR_R_05_Approval_blocked_until_override_set`; `Dom: BR_R_05_Exactly_10M_is_allowed`; `Dom: BR_R_05_Subrogation_excluded_from_aggregate`; `Int: BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit` | Planned |
| BR-R-06 | GL idempotency key `Reserve:{ComponentId}:Change:{Seq}`; re-entrant, no duplicate audit | FRS §7.2, §6.5, §12.1; Brief BR-R-05; D-23 | Domain (key), DB (unique filtered index), Infra (job) | `Dom: BR_R_06_Idempotency_key_format`; `Int: BR_R_06_Job_run_twice_writes_one_gl_audit_entry`; `Int: BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry` | Planned |
| BR-ST-01 | Only §4.2 transitions; invalid → 422 listing the valid next statuses | FRS §7.3, §4.2, §8; D-09 | Domain (transition table) | `Dom: BR_ST_01_Invalid_transition_lists_valid_next_statuses`; `Int: BR_ST_01_Invalid_transition_returns_422` | Planned |
| BR-ST-02 | → Open requires no Critical issues (and BR-C-02 acknowledged) + ≥1 Claimant (+ handler set, D-18) | FRS §7.3, §4.1, §4.2; D-18, D-19 | Domain | `Dom: BR_ST_02_Open_with_unresolved_critical_is_rejected`; `Dom: BR_ST_02_Open_without_handler_is_rejected`; `Dom: BR_ST_02_Open_with_all_conditions_met_succeeds` | Planned |
| BR-ST-03 | → Closed requires CC-01..04; 422 listing every blocking condition | FRS §7.3, §4.3 | Domain | `Dom: BR_ST_03_Close_lists_all_failed_conditions` | Planned |
| BR-ST-04 | Reopen: Supervisor+ and a non-empty reason; immediately → Open | FRS §7.3, §4.2; D-09, D-26 | API (policy), Domain | `Dom: BR_ST_04_Reopen_moves_claim_to_open`; `Dom: BR_ST_04_Reopen_without_reason_is_rejected`; `Int: BR_ST_04_Handler_reopen_returns_403`; `Int: BR_ST_04_Reopen_writes_three_audit_rows` | Planned |
| CC-01 | No PendingApproval reserve txns at closure | FRS §4.3; D-21 | Domain | `Dom: CC_01_Close_with_pending_reserve_is_rejected` | Planned |
| CC-02 | No unresolved Critical issues at closure | FRS §4.3; D-07 | Domain | `Dom: CC_02_Close_with_open_critical_issue_is_rejected` | Planned |
| CC-03 | ≥1 active Claimant at closure | FRS §4.3 | Domain | `Dom: CC_03_Close_without_active_claimant_is_rejected` | Planned |
| CC-04 | CurrentAmount > 0 → warning; closure requires a justification note | FRS §4.3; D-26 | Domain, UI (pre-flight checklist) | `Dom: CC_04_Close_with_open_reserves_without_justification_is_rejected`; `Dom: CC_04_Close_with_open_reserves_and_justification_succeeds` | Planned |
| BR-D-01 | Blob path `{org}/{claim}/{…filename}` in `claim-documents`; filename sanitised | FRS §7.4, §13; Brief §3.6; D-28 | Infra (storage), Domain (FileName VO) | `Dom: BR_D_01_Path_traversal_is_stripped` (`../`, absolute, `\`, unicode); `Int: BR_D_01_Blob_is_stored_under_org_and_claim_prefix` (Azurite) | Planned |
| BR-D-02 | Retrieval = SAS URL, 1h TTL; bytes never proxied (Azure) | FRS §7.4, §13; Brief §3.6 | Infra | `Int: BR_D_02_Sas_url_expires_in_one_hour`; `Int: BR_D_02_List_documents_returns_sas_not_bytes` | Planned |
| BR-D-03 | Fallback to local FS via `IStorageService`; provider chosen in appsettings | FRS §7.4, §13; Brief §3.6; D-28 | Infra, API (DI) | `Int: BR_D_03_LocalFileSystem_provider_round_trip`; `Int: BR_D_03_Provider_selected_from_config` | Planned |
| BR-P-01 | Zero or more parties; ≥1 Claimant before Open (Critical) | FRS §7.5 | Domain | covered by BR-C-03 tests + `Dom: BR_P_01_Claim_can_exist_with_zero_parties` | Planned |
| BR-P-02 | Roles Claimant/Insured/ThirdParty/Witness/Attorney; duplicates allowed | FRS §7.5, §9.3 | Validator (enum), Domain | `Dom: BR_P_02_Multiple_claimants_are_allowed`; `App: BR_P_02_Unknown_party_role_is_rejected` | Planned |
| PTY-01 | Removing the last active Claimant → 422 (soft-remove via IsActive) | FRS §10.1, §11.3; D-27 | Domain, UI (disabled button) | `Dom: PTY_01_Removing_last_active_claimant_is_rejected`; `Dom: PTY_01_Removed_party_is_inactive_not_deleted` | Planned |
| BR-A-01 | Audit log append-only; no UPDATE/DELETE | FRS §7.6, §14.2; D-14 | Persistence (interceptor), DB (trigger/DENY) | `Int: BR_A_01_Modifying_audit_entry_throws`; `Int: BR_A_01_Deleting_audit_entry_throws`; `Int: BR_A_01_Db_trigger_blocks_raw_update` | Planned |
| BR-A-02 | Every significant action writes an audit entry | FRS §7.6, §14.1 | Application (before-commit handlers) | see AUD-01..16 | Planned |

---

## 3. Status transitions (FRS §4.2) — seeded `ClaimStatusTransitions` (D-09)

| ID | Transition | Conditions | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|---|
| TR-00 | Any unlisted transition → 422 with valid next statuses | — | FRS §4.2, BR-ST-01 | Domain | `Dom: TR_00_Unlisted_transition_is_rejected` (theory over all pairs) | Planned |
| TR-01 | Draft → Open | BR-ST-02, D-18, D-19 | FRS §4.2 | Domain | `Dom: TR_01_Draft_to_open` | Planned |
| TR-02 | Open → UnderInvestigation | none | FRS §4.2 | Domain | `Dom: TR_02_Open_to_under_investigation` | Planned |
| TR-03 | Open → PendingPayment | ≥1 approved reserve txn | FRS §4.2; D-21 | Domain | `Dom: TR_03_Open_to_pending_payment_requires_approved_reserve` | Planned |
| TR-04 | Open → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_04_Open_to_closed` | Planned |
| TR-05 | Open → Withdrawn | reason required; no pending txns (D-26) | FRS §4.2 | Domain | `Dom: TR_05_Open_to_withdrawn_requires_reason` | Planned |
| TR-06 | UnderInvestigation → Open | none | FRS §4.2 | Domain | `Dom: TR_06_Under_investigation_to_open` | Planned |
| TR-07 | UnderInvestigation → PendingPayment | ≥1 approved reserve txn | FRS §4.2 | Domain | `Dom: TR_07_Under_investigation_to_pending_payment_requires_approved_reserve` | Planned |
| TR-08 | UnderInvestigation → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_08_Under_investigation_to_closed` | Planned |
| TR-09 | UnderInvestigation → Withdrawn | reason required | FRS §4.2 | Domain | `Dom: TR_09_Under_investigation_to_withdrawn_requires_reason` | Planned |
| TR-10 | PendingPayment → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_10_Pending_payment_to_closed` | Planned |
| TR-11 | Closed → Reopened | supervisor+, reason | FRS §4.2, BR-ST-04 | Domain, API | see BR-ST-04 | Planned |
| TR-12 | Reopened → Open | automatic, same command | FRS §4.2, BR-ST-04 | Domain | see BR-ST-04 | Planned |
| TR-13 | Closed/Withdrawn claims are read-only (reserves, parties, docs, policy, notes) | NOT SPECIFIED → D-26 | D-26 | Domain | `Dom: TR_13_Changes_on_closed_claim_are_rejected` | Planned |

---

## 4. Validation messages (FRS §8) — exact wording asserted in tests

| ID | Field | Rule | Severity | Message (verbatim) | Enforced in | Planned test | Status |
|---|---|---|---|---|---|---|---|
| VAL-01 | LossDate | not in the future | Critical | Loss date cannot be in the future. | Validator | `App: VAL_01_message` | Planned |
| VAL-02 | LossDate | valid/required | Critical | Loss date is required. | Validator | `App: VAL_02_message` | Planned |
| VAL-03 | LossDate | within policy period | Warning | Loss date is outside the policy effective period. | Domain (issue) | `Dom: VAL_03_message` | Planned |
| VAL-04 | LossDescription | required, ≥20 | Critical | Loss description is required and must be at least 20 characters. | Validator | `App: VAL_04_message` | Planned |
| VAL-05 | CauseOfLossCode | exists & active | Critical | Cause of loss code is not recognised or is inactive. | Validator | `App: VAL_05_message` | Planned |
| VAL-06 | PolicyId | null → warning | Warning | No policy linked. Policy must be associated before reserves can be set. | Domain (issue) | `Dom: VAL_06_message` | Planned |
| VAL-07 | ClaimParties | ≥1 Claimant before Open | Critical | At least one Claimant party is required to open a claim. | Domain | `Dom: VAL_07_message` | Planned |
| VAL-08 | ReserveAmount | > 0 unless Subrogation | Critical | Reserve amount must be greater than zero. | Validator | `App: VAL_08_message` | Planned |
| VAL-09 | ReserveComponent | defined type | Critical | Invalid reserve component type. | Validator | `App: VAL_09_message` | Planned |
| VAL-10 | ReserveAmount | aggregate ≤ $10M | Warning | Total reserves will exceed $10,000,000. Manager override required. | Domain | `Dom: VAL_10_message` | Planned |
| VAL-11 | StatusTransition | valid next status | Critical | Transition from {from} to {to} is not permitted. | Domain | `Dom: VAL_11_message` | Planned |
| VAL-12 | StatusTransition | closure conditions | Critical | Claim cannot be closed — {condition} is not satisfied. | Domain | `Dom: VAL_12_message` | Planned |
| VAL-13 | ReserveApproval | role authority | Critical | Your role does not have authority to approve this reserve amount. | Domain | `Dom: VAL_13_message` | Planned |
| VAL-14 | ReserveApproval | not submitter | Critical | Self-approval is not permitted. | Domain | `Dom: VAL_14_message` | Planned |

Note: FRS §7.1 BR-C-06 words the no-policy warning differently ("No policy linked — claim requires policy association before financial actions are
permitted."). The §8 table's wording (VAL-06) is used because §8 is the consolidated message table. Recorded under D-02.

---

## 5. Reserve workflow (FRS §6) beyond the BR-R rules

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| RSV-01 | Components: Indemnity/Expense/ALAE/SubrogationRecoverable; several per claim | FRS §6.2; D-03 | Domain, Validator | `Dom: RSV_01_Claim_can_hold_multiple_components` | Planned |
| RSV-02 | PendingApproval txn is immutable; submitter may retract → Cancelled | FRS §6.4 rule box; §10.2 | Domain | `Dom: RSV_02_Retract_by_submitter_cancels_txn`; `Dom: RSV_02_Retract_by_other_user_is_rejected`; `Dom: RSV_02_Retract_non_pending_is_rejected` | Planned |
| RSV-03 | Approve → Approved + GL enqueued (after commit); Reject → Rejected + reason | FRS §6.4 steps 8–9 | Domain, Application (after-commit) | `Dom: RSV_03_Approve_sets_approved_and_raises_event`; `Dom: RSV_03_Reject_requires_reason`; `Int: RSV_03_Approval_enqueues_gl_job_after_commit` | Planned |
| RSV-04 | Event-sourced: INSERT history rows; CurrentAmount = Σ approved; never UPDATE amounts | FRS §6.6, §9.5, §9.6; D-11, D-22 | Domain, Persistence (interceptor guards amount columns) | `Dom: RSV_04_Current_amount_is_sum_of_approved_txns`; `Int: RSV_04_Modifying_history_amount_throws` | Planned |
| RSV-05 | ChangeSequence monotonic per component, protected by RowVer | FRS §9.6, §15.1; D-23 | Domain, DB (RowVer) | `Dom: RSV_05_Change_sequence_increments_per_component`; `Int: RSV_05_Concurrent_submissions_on_same_component_one_gets_409` | Planned |
| RSV-06 | At most one PendingApproval per component | D-22 (ASSUMPTION) | Domain, DB (unique filtered index) | `Dom: RSV_06_Second_pending_on_component_is_rejected` | Planned |
| RSV-07 | Concurrent approvals of the same txn → exactly one succeeds (409 for the other) | FRS §15.1 RowVer; PROMPTS Phase 4 | Domain + DB (RowVer on Claim/component) | `Int: RSV_07_Concurrent_approvals_one_wins` | Planned |
| RSV-08 | History stores previous/new balance, reason, submitter, approver, timestamps, posting status | FRS §6.6, §9.6 | Domain | `Dom: RSV_08_History_row_captures_balances_and_actors` | Planned |
| RSV-09 | Authority threshold preview in UI (Step 3 and the Add Reserve panel) | FRS §5.2, §11.2, §11.3 | UI | `Web: RSV_09_Authority_indicator_boundaries` | Planned |

---

## 6. API endpoints

| ID | Endpoint | Behaviour | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|---|
| API-01 | `POST /api/claims` | FNOL; single txn; claim number; LossEvent, parties, risk objects, optional reserve; 201 + ClaimNumber + Location; `ClaimCreated` event | FRS §10.1, §5.2; Brief §3.3.1 | API, Application, Domain | `Int: API_01_Create_claim_returns_201_with_claim_number`; `Int: API_01_Create_is_atomic_on_failure` | Planned |
| API-02 | `GET /api/claims` | Paged; filters status (multi), dateFrom/dateTo, assignedHandlerId, causeOfLossCode, policyId, search; summary rows incl. totalReserves | FRS §10.1; Brief §3.3.1; D-29 | Application (projection) | `Int: API_02_List_filters_and_paging` (one case per filter) | Planned |
| API-03 | `GET /api/claims/{id}` | Detail incl. parties, risk objects, reserve summary, documents, recent audit | FRS §10.1; Brief §3.3.1 | Application | `Int: API_03_Get_claim_detail`; `Int: API_03_Other_org_claim_returns_404` | Planned |
| API-04 | `PUT /api/claims/{id}/status` | `{targetStatus, reason?, justification?}`; 422 with blocking list | FRS §10.1; Brief §3.3.1; D-26 | API, Domain | covered by TR-* / BR-ST-* | Planned |
| API-05 | `GET /api/claims/{id}/audit` | Paged, reverse-chronological | FRS §10.1; Brief §3.3.1 | Application | `Int: API_05_Audit_is_reverse_chronological_and_paged` | Planned |
| API-06 | `POST /api/claims/{id}/parties` | Add party; PARTY_ADDED | FRS §10.1 | Application, Domain | `Int: API_06_Add_party` | Planned |
| API-07 | `DELETE /api/claims/{id}/parties/{partyId}` | Soft remove (IsActive=false); 422 for the last Claimant | FRS §10.1 | Domain | see PTY-01 | Planned |
| API-08 | `POST /api/claims/{id}/documents` | Multipart upload; blob + metadata; DOCUMENT_UPLOADED | FRS §10.1, §13; Brief §3.3.1 | Infra, Application | `Int: API_08_Upload_document` | Planned |
| API-09 | `GET /api/claims/{id}/documents` | List with 1h SAS URL per doc | FRS §10.1; Brief §3.3.1 | Infra, Application | see BR-D-02 | Planned |
| API-10 | `POST /api/claims/{id}/reserves` | Open/adjust; `{component, amount, changeReason, transactionType?}`; 201 + txn + approval status; enqueue if auto-approved | FRS §10.2; Brief §3.3.3; D-04, D-05 | API, Domain | `Int: API_10_Submit_auto_approved_reserve`; `Int: API_10_Submit_pending_reserve` | Planned |
| API-11 | `PUT /api/claims/{id}/reserves/{componentId}` | Adjust to newAmount (Adjust txn, delta computed) | Brief §3.3.3; D-04 | Application | `Int: API_11_Put_reserve_creates_adjust_txn_with_delta` | Planned |
| API-12 | `GET /api/claims/{id}/reserves` | Summary per component (current, pending) + full history | FRS §10.2; Brief §3.3.3 | Application | `Int: API_12_Get_reserves_summary_and_history` | Planned |
| API-13 | `POST /api/claims/{id}/reserves/{txnId}/approve` | Supervisor+; not self; enqueue GL | FRS §10.2; Brief §3.3.3 | API policy, Domain | see BR-R-02/03, RSV-03 | Planned |
| API-14 | `POST /api/claims/{id}/reserves/{txnId}/reject` | Supervisor+; `{rejectionReason}` | FRS §10.2; Brief §3.3.3 | API policy, Domain | `Int: API_14_Reject_requires_reason` | Planned |
| API-15 | `POST /api/claims/{id}/reserves/{txnId}/retract` | Submitter only; → Cancelled | FRS §10.2 | Domain | see RSV-02 | Planned |
| API-16 | `GET /api/reference/cause-of-loss-codes?perilCategory=` | Active codes, filterable | FRS §10.3; Brief §3.3.4 | Application | `Int: API_16_Cause_codes_filtered_by_peril` | Planned |
| API-17 | `GET /api/reference/claim-statuses` | Statuses + valid next transitions | FRS §10.3; Brief §3.3.4 | Application (from table) | `Int: API_17_Statuses_include_transitions` | Planned |
| API-18 | `GET /api/policies/search?q=` | Search by number or client name; dates, status, coverage | FRS §10.3; Brief §3.3.2 | Application | `Int: API_18_Policy_search_by_number_and_name` | Planned |
| API-19 | `GET /api/policies/{id}/coverage` | Coverage types for a policy | Brief §3.3.2; D-08 | Application | `Int: API_19_Policy_coverage` | Planned |
| API-20 | `POST /api/claims/{id}/validate` | Re-evaluate rules; persist issue changes | FRS §5.4; D-08 | Application, Domain | `Int: API_20_Validate_resolves_fixed_issues` | Planned |
| API-21 | `PUT /api/claims/{id}/policy` | Link/change policy; re-evaluate BR-C-02/06 | BR-C-06; D-08 | Domain | see BR-C-06 | Planned |
| API-22 | `PUT /api/claims/{id}/assignee` | Supervisor+; same-org active user | FRS §4.1; D-18 | API policy, Domain | `Int: API_22_Assign_handler`; `Int: API_22_Handler_cannot_reassign_403` | Planned |
| API-23 | `PATCH /api/claims/{id}` | Notes, severity | FRS §11.3 Tab 1; D-08 | Domain | `Int: API_23_Update_notes` | Planned |
| API-24 | `PUT /api/claims/{id}/reserve-limit-override` | Manager only | BR-R-05, FRS §3; D-08 | API policy, Domain | `Int: API_24_Only_manager_sets_override` | Planned |
| API-25 | `POST /api/claims/{id}/reserves/{txnId}/retry-posting` | Failed → re-enqueue | FRS §11.3; D-08 | Application | `Int: API_25_Retry_failed_posting` | Planned |
| API-26 | `POST /api/claims/{id}/validation-issues/{issueId}/acknowledge` | Warnings only, with note | BR-C-02; D-07 | Domain | see BR-C-02 | Planned |
| API-27 | `GET /api/claims/{id}/documents/{docId}/url` | Fresh SAS | FRS §11.3 Tab 4; D-08 | Infra | `Int: API_27_Fresh_document_url` | Planned |
| API-28 | `POST /api/auth/dev-token`, `GET /api/auth/users` | Signed JWT for a seeded user; config-gated | FRS §3, §11.4; D-16 | Infra | `Int: API_28_Dev_token_is_valid_bearer`; `Int: API_28_Disabled_when_flag_off` | Planned |
| API-29 | `GET /api/users?role=` | Handler list for filter/assign | FRS §11.1; D-29 | Application | `Int: API_29_List_handlers` | Planned |
| API-ERR | Errors: ProblemDetails; 422 `{type,title,status,errors{field:[msgs]}}`; 404/403/409 mapping | FRS §10.4; CLAUDE.md rule 12 | API middleware | `Int: API_ERR_422_body_matches_frs_10_4`; `Int: API_ERR_Concurrency_returns_409`; `Int: API_ERR_Not_found_returns_404` | Planned |
| API-IDEMP | `Idempotency-Key` on writes replays the stored response | FRS §10; D-24 | API filter, DB | `Int: API_IDEMP_Repeated_key_replays_response`; `Int: API_IDEMP_Same_key_different_body_returns_422` | Planned |
| API-AUTH | Bearer JWT on all endpoints except the dev token and Swagger | FRS §10; Brief §3.7.4 | API | `Int: API_AUTH_Missing_token_returns_401` | Planned |
| API-CORR | `X-Correlation-Id` read or created; echoed; stamped on audit rows | FRS §14.2; CLAUDE.md rule 6 | API middleware | `Int: API_CORR_Correlation_id_is_stamped_on_audit_rows` | Planned |
| API-DOCS | Swagger/OpenAPI populated, with a JWT scheme, reachable when deployed | Brief §4.3, §6.2 | API | `Manual: Swagger reachable on Azure URL` | Planned |

---

## 7. Background jobs (FRS §12, Brief §3.5)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| JOB-01 | `PostGLReserveChangeJob(reserveHistoryId, claimId, idempotencyKey)` enqueued on approval (auto/manual), after commit | FRS §12.1, §6.5; Brief §3.5; D-15 | Application (after-commit handler), Infra | `App: JOB_01_Auto_approval_enqueues_after_commit`; `App: JOB_01_Rollback_enqueues_nothing` | Planned |
| JOB-02 | Idempotent: conditional update `WHERE PostingStatus <> 'Posted'`; audit only if 1 row changed; one txn | FRS §12.1; BR-R-06; CLAUDE.md | Infra, DB | see BR-R-06 | Planned |
| JOB-03 | Re-entrant: retry after partial failure creates no duplicates | FRS §6.5, §12.1 | Infra | `Int: JOB_03_Retry_after_failure_writes_single_entry` | Planned |
| JOB-04 | GL_POSTING_SIMULATED audit: journal "DR Change in Outstanding Reserves / CR Outstanding Loss Reserves", Amount, in NewValue | FRS §6.5, §14.1 | Infra | `Int: JOB_04_Gl_audit_contains_journal_lines` | Planned |
| JOB-05 | On success: PostingStatus=Posted, PostingJobId set | FRS §6.5, §12.1 | Infra | `Int: JOB_05_Success_sets_posted_and_job_id` | Planned |
| JOB-06 | After retries exhausted: PostingStatus=Failed + GL_POSTING_FAILED (reason in NewValue) | FRS §12.1, §14.1; D-35 | Infra | `Int: JOB_06_Final_attempt_failure_marks_failed_and_audits` | Planned |
| JOB-07 | `SlaMonitoringJob` recurring `*/15 * * * *`, registered at startup (while scaled to zero it does not run; the missed occurrence fires on wake, D-36) | FRS §12.2; Brief §3.5; D-36 | Infra, API startup | `Int: JOB_07_Recurring_jobs_registered_at_startup` | Planned |
| JOB-08 | Draft/Open with COALESCE(UpdatedAt, CreatedAt) older than 48h → SLA_BREACH_DETECTED "Claim has not been updated in 48 hours" | FRS §12.2; D-01 | Infra | `Int: JOB_08_Stale_draft_and_open_claims_are_flagged`; `Int: JOB_08_Never_updated_claim_is_flagged`; `Int: JOB_08_Under_investigation_claim_is_ignored` | Planned |
| JOB-09 | ≤1 breach event per claim per 24h | FRS §12.2 | Infra | `Int: JOB_09_Second_run_within_24h_adds_nothing`; `Int: JOB_09_Run_after_24h_adds_new_entry` (fake TimeProvider) | Planned |
| JOB-10 | SLA job never changes Status or bumps UpdatedAt | FRS §12.2; D-01 | Infra | `Int: JOB_10_Sla_job_does_not_modify_claim_row` | Planned |
| JOB-11 | GL sweeper re-enqueues approved-but-unposted rows older than 5 min | D-15 | Infra | `Int: JOB_11_Sweeper_enqueues_stranded_postings` | Planned |
| JOB-12 | Jobs run with an explicit tenant scope and their own correlation id | D-31; CLAUDE.md rule 6 | Infra | `Int: JOB_12_Job_sees_only_its_tenant_and_no_deleted_rows` | Planned |

---

## 8. Documents (FRS §13, Brief §3.6)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| DOC-01 | Container `claim-documents`; path per BR-D-01 (D-28) | FRS §13; Brief §3.6 | Infra | see BR-D-01 | Planned |
| DOC-02 | SAS 1h TTL; frontend opens the URL in a new tab | FRS §13 | Infra, UI | see BR-D-02 | Planned |
| DOC-03 | Local fallback under `/uploads/{org}/{claim}/`, local download URL | FRS §13; D-28 | Infra | see BR-D-03 | Planned |
| DOC-04 | `IStorageService` with Azure + Local implementations; `Storage:Provider` | FRS §13; Brief §3.6, §6.1 | Application (interface), Infra | see BR-D-03 | Planned |
| DOC-05 | MIME allowlist PDF/JPEG/PNG/DOCX/XLSX/TXT/CSV (declared + signature) | FRS §13 | Validator, Infra | `App: DOC_05_Disallowed_mime_is_rejected`; `App: DOC_05_Spoofed_extension_is_rejected` | Planned |
| DOC-06 | 50 MB limit (Kestrel + validator) | FRS §13; D-28 | API config, Validator | `Int: DOC_06_File_over_50mb_is_rejected` | Planned |
| DOC-07 | DOCUMENT_UPLOADED with RelatedEntityId = documentId | FRS §13, §14.1 | Application | `Int: DOC_07_Upload_writes_audit_with_document_id` | Planned |
| DOC-08 | Metadata row per §9.7 (name, type, path, content type, size, uploader) | FRS §9.7; Brief §3.2 | Persistence | covered by API-08 | Planned |

---

## 9. Audit log (FRS §14)

| ID | Event type | Trigger | Source | Planned test | Status |
|---|---|---|---|---|---|
| AUD-01 | CLAIM_CREATED | POST /claims | FRS §14.1; Brief §6.1 | `Int: AUD_01_Claim_created_logged` | Planned |
| AUD-02 | STATUS_CHANGED | every transition; Old/New = statuses | FRS §14.1 | `Dom/Int: AUD_02_Status_change_logged_with_old_and_new` | Planned |
| AUD-03 | PARTY_ADDED | add party (incl. at intake) | FRS §14.1 | `Int: AUD_03_Party_added_logged` | Planned |
| AUD-04 | PARTY_REMOVED | soft remove | FRS §14.1 | `Int: AUD_04_Party_removed_logged` | Planned |
| AUD-05 | RESERVE_CREATED | every submission | FRS §14.1 | `Int: AUD_05_Reserve_created_logged` | Planned |
| AUD-06 | RESERVE_AUTO_APPROVED | ≤ $10k | FRS §14.1 | `Int: AUD_06_Auto_approval_logged` | Planned |
| AUD-07 | RESERVE_APPROVED | manual approval | FRS §14.1 | `Int: AUD_07_Approval_logged` | Planned |
| AUD-08 | RESERVE_REJECTED | rejection; OldValue includes the reason | FRS §14.1 | `Int: AUD_08_Rejection_logged_with_reason` | Planned |
| AUD-09 | RESERVE_RETRACTED | retract | FRS §14.1 | `Int: AUD_09_Retract_logged` | Planned |
| AUD-10 | GL_POSTING_SIMULATED | GL job success | FRS §14.1 | see JOB-04 | Planned |
| AUD-11 | GL_POSTING_FAILED | GL job exhausted | FRS §14.1 | see JOB-06 | Planned |
| AUD-12 | DOCUMENT_UPLOADED | upload | FRS §14.1 | see DOC-07 | Planned |
| AUD-13 | CLAIM_CLOSED | → Closed; NewValue has the reason (+ justification) | FRS §14.1; D-26 | `Int: AUD_13_Claim_closed_logged` | Planned |
| AUD-14 | CLAIM_REOPENED | Closed → Reopened; NewValue has the reason | FRS §14.1; D-26 | see BR-ST-04 | Planned |
| AUD-15 | SLA_BREACH_DETECTED | SLA job | FRS §14.1 | see JOB-08 | Planned |
| AUD-16 | VALIDATION_ISSUE_ADDED | new issue recorded | FRS §14.1; D-07 | `Int: AUD_16_Validation_issue_logged` | Planned |
| AUD-17 | Additional events: RISK_OBJECT_ADDED, POLICY_LINKED, HANDLER_ASSIGNED, CLAIM_UPDATED, RESERVE_LIMIT_OVERRIDE_SET, GL_POSTING_RETRIED, VALIDATION_ISSUE_RESOLVED/ACKNOWLEDGED | BR-A-02; D-08 | `Int: AUD_17_*` (one per event) | Planned |
| AUD-I1 | All writes via `IAuditLogService`; no direct DbContext writes from handlers | FRS §14.2 | Architecture test `AUD_I1_Only_audit_service_writes_audit_log` (NetArchTest) | Planned |
| AUD-I2 | Timestamps DATETIMEOFFSET(7) UTC | FRS §14.2 | `Int: AUD_I2_Audit_created_at_is_utc` | Planned |
| AUD-I3 | Request CorrelationId on all audit rows of a request | FRS §14.2 | see API-CORR | Planned |
| AUD-I4 | Audit write is in the same transaction as the state change | CLAUDE.md rule 5 | `Int: AUD_I4_Failed_command_leaves_no_audit_row` | Planned |

---

## 10. Roles & security (FRS §3, Brief §3.7.4)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| SEC-01 | Three roles handler/supervisor/manager, hierarchical | FRS §3 | Infra (JWT), API policies | `Int: SEC_01_Role_policies_are_hierarchical` | Planned |
| SEC-02 | Backend validates the caller's role on approval endpoints | FRS §3 | API policy + Domain | `Int: SEC_02_Handler_approve_returns_403` | Planned |
| SEC-03 | Approve/Reject visible only to supervisor/manager | FRS §3, §11.3; Brief §3.7.4 | UI | `Web: SEC_03_Approve_buttons_hidden_for_handler` | Planned |
| SEC-04 | Tenant isolation: data from another org is invisible | FRS §15.1; D-12 | Persistence (global filter) | `Int: SEC_04_Cross_tenant_claim_is_not_found` | Planned |
| SEC-05 | Test users: 2 per role | Brief §4.3; D-16 | Seed | `Int: SEC_05_Seeded_users_present` | Planned |

---

## 11. Data conventions (FRS §15, Brief §3.2, A.4)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| CONV-01 | GUID PKs, default `NEWSEQUENTIALID()` (domain-assigned sequential values, D-30) | FRS §15.1, §15.2 | Persistence config | `Int: CONV_01_All_pks_have_newsequentialid_default` (model metadata scan) | Planned |
| CONV-02 | Money DECIMAL(19,4) via `HasPrecision(19,4)`; no float/money | FRS §15.1, §15.2 | Persistence config | `Int: CONV_02_All_decimals_are_19_4` | Planned |
| CONV-03 | Timestamps DATETIMEOFFSET(7), UTC | FRS §15.1 | Persistence config | `Int: CONV_03_All_timestamps_are_datetimeoffset` | Planned |
| CONV-04 | Short codes NVARCHAR(50); names NVARCHAR(255); free text NVARCHAR(MAX); enums as NVARCHAR(50) | FRS §15.1; CLAUDE.md rule 10 | Persistence config | `Int: CONV_04_Enums_stored_as_nvarchar_50` | Planned |
| CONV-05 | Soft delete (IsDeleted + DeletedAt) + global filter on business tables (not the audit log, D-14) | FRS §15.1, §15.2 | Persistence | `Int: CONV_05_Soft_deleted_rows_are_filtered` | Planned |
| CONV-06 | Audit columns filled by an interceptor | FRS §15.1 | Persistence interceptor | `Int: CONV_06_Audit_columns_populated` | Planned |
| CONV-07 | OrganisationId on every business table + global filter | FRS §15.1; D-12 | Persistence | see SEC-04 | Planned |
| CONV-08 | RowVer on Claims and ClaimReserveComponents | FRS §15.1 | Persistence | see RSV-05/07 | Planned |
| CONV-09 | Fluent API only (`IEntityTypeConfiguration<T>`), no schema annotations | FRS §15.2 | Persistence | Architecture test `CONV_09_No_data_annotations_in_domain` | Planned |
| CONV-10 | Seed via HasData/migrations (policies §5.5, codes §5.6, transitions, users, org) — never at startup | FRS §15.4, §5.5, §5.6; Brief §4.1 | Persistence | `Int: CONV_10_Seed_data_matches_frs_5_5_and_5_6` | Planned |
| CONV-11 | `dotnet ef database update` builds a fresh DB from zero; app starts with no manual setup | FRS §15.4 | Persistence | `Int: CONV_11_Migrations_apply_to_empty_database` | Planned |
| CONV-12 | No hard-coded GUIDs in application logic | FRS §15.4 | Code review + grep | `Manual: CONV_12_grep_for_guid_literals_outside_seed` | Planned |
| CONV-13 | Naming: tables/columns PascalCase; VerbNounCommand; Get/List…Query; kebab-case routes | FRS §15.3 | Architecture test | `CONV_13_Commands_and_queries_follow_naming` | Planned |
| CONV-14 | Clean Architecture dependency rule | Brief §2.4; CLAUDE.md | Architecture test | `CONV_14_Layer_dependencies_are_respected` | Planned |
| CONV-15 | FluentValidation in the MediatR pipeline; AutoMapper profiles in Application; UoW coordinates each command | Brief §2.4, §6.1 | Application | `App: CONV_15_Validation_behavior_short_circuits_handler`; architecture test for profile location | Planned |

---

## 12. Frontend (FRS §11, Brief §3.7)

| ID | Requirement | Source | Planned test | Status |
|---|---|---|---|---|
| UI-LIST-01 | Paginated table: Claim No, Policy No, Client, Loss Date, Cause, Status badge, Total Reserve | FRS §11.1; Brief §3.7.1 | `Web: UI_LIST_01_Columns_rendered` | Planned |
| UI-LIST-02 | Badge colours: Draft grey, Open blue, UnderInvestigation orange, PendingPayment purple, Closed green, Reopened amber, Withdrawn dark grey | FRS §11.1 | `Web: UI_LIST_02_Status_badge_colours` | Planned |
| UI-LIST-03 | Filters: status multi-select, loss date range, handler, cause | FRS §11.1; Brief §3.7.1 | `Web: UI_LIST_03_Filters_map_to_query_params` | Planned |
| UI-LIST-04 | Row click → detail | FRS §11.1 | `Manual` | Planned |
| UI-LIST-05 | "Log New Claim" primary button → FNOL | FRS §11.1 | `Manual` | Planned |
| UI-LIST-06 | Total count + page-size selector | FRS §11.1 | `Manual` | Planned |
| UI-LIST-07 | Empty state | FRS §11.1 | `Web: UI_LIST_07_Empty_state_shown` | Planned |
| UI-FNOL-01 | 3-step stepper; each step validates independently | FRS §11.2; Brief §3.7.2 | `Web: UI_FNOL_01_Cannot_advance_with_invalid_step` | Planned |
| UI-FNOL-02 | Policy typeahead (number/name) populating number, client, dates | FRS §11.2 | `Web: UI_FNOL_02_Typeahead_debounced_search` | Planned |
| UI-FNOL-03 | In-force badge: green inside the period, amber otherwise | FRS §11.2 | `Web: UI_FNOL_03_In_force_badge` | Planned |
| UI-FNOL-04 | Unknown Policy toggle | FRS §11.2 | `Web: UI_FNOL_04_Unknown_policy_disables_policy_and_reserve` | Planned |
| UI-FNOL-05 | Loss date picker; future-date error | FRS §11.2 | `Web: UI_FNOL_05_Future_date_validator` | Planned |
| UI-FNOL-06 | Searchable cause dropdown from the API | FRS §11.2 | `Manual` | Planned |
| UI-FNOL-07 | Description textarea with a 20-char counter | FRS §11.2 | `Web: UI_FNOL_07_Min_length_counter` | Planned |
| UI-FNOL-08 | Location, estimated amount (optional) | FRS §11.2 | `Manual` | Planned |
| UI-FNOL-09 | Parties FormArray; Claimant-required warning; chips/cards with remove | FRS §11.2; Brief §3.7.2 | `Web: UI_FNOL_09_Claimant_required_validator` | Planned |
| UI-FNOL-10 | Risk objects FormArray; advisory if none | FRS §11.2 | `Web: UI_FNOL_10_Risk_object_advisory` | Planned |
| UI-FNOL-11 | Optional initial reserve + live authority indicator | FRS §11.2; Brief §3.7.2 | see RSV-09 | Planned |
| UI-FNOL-12 | Review summary; Create disabled until steps 1–2 valid; confirm dialog on warnings | FRS §11.2 | `Web: UI_FNOL_12_Warning_confirmation_dialog` | Planned |
| UI-FNOL-13 | Success → detail + snackbar with claim number | FRS §11.2; Brief §3.7.2 | `Manual` | Planned |
| UI-FNOL-14 | Server 422 errors shown inline and at the top of the step | FRS §11.2 | `Web: UI_FNOL_14_Server_errors_mapped_to_controls` | Planned |
| UI-DET-01 | Header: copyable claim no, status chip, policy/client, loss date/cause, handler | FRS §11.3; Brief §3.7.3 | `Manual` | Planned |
| UI-DET-02 | Transition menu of valid next statuses; confirm dialog; Closed pre-flight checklist | FRS §11.3; Brief §3.7.3 | `Web: UI_DET_02_Menu_shows_only_valid_next_statuses` | Planned |
| UI-DET-03 | Tab 1 Overview: loss details, severity badge, estimate, editable notes | FRS §11.3 | `Manual` | Planned |
| UI-DET-04 | Tab 2 Parties: list with role badge/contact/active; add inline; remove disabled for last Claimant | FRS §11.3 | `Web: UI_DET_04_Remove_disabled_for_last_claimant` | Planned |
| UI-DET-05 | Tab 3 Reserves: summary cards (current + pending, amber) | FRS §11.3 | `Manual` | Planned |
| UI-DET-06 | History table: Date, Type, Component, Amount ±colour, Status, Submitted By, Approved By | FRS §11.3 | `Manual` | Planned |
| UI-DET-07 | Add Reserve slide-in panel with authority indicator | FRS §11.3 | see RSV-09 | Planned |
| UI-DET-08 | Approve/Reject on pending rows for supervisor/manager only | FRS §11.3; Brief §3.7.3 | see SEC-03 | Planned |
| UI-DET-09 | Retract for the submitter's own pending rows | FRS §11.3 | `Web: UI_DET_09_Retract_only_for_submitter` | Planned |
| UI-DET-10 | GL badge Pending/Posted/Failed + retry | FRS §11.3 | `Manual` | Planned |
| UI-DET-11 | Tab 4 Documents: list, download via SAS in a new tab, upload with progress | FRS §11.3; Brief §3.7.3 | `Manual` | Planned |
| UI-DET-12 | Tab 5 Audit: reverse-chronological, paged, event badge, user, related link; read-only | FRS §11.3; Brief §3.7.3 | `Manual` | Planned |
| UI-DET-13 | Validation issues visible with Acknowledge (D-07) | D-07 | `Manual` | Planned |
| UI-GEN-01 | Lazy-loaded features: claims-list, fnol-intake, claim-detail | FRS §11.4; Brief §3.7.4; D-17 | `Manual: build output shows 3 lazy chunks` | Planned |
| UI-GEN-02 | Typed service layer only; no HttpClient in components | FRS §11; Brief §3.7 | lint rule / architecture spec | Planned |
| UI-GEN-03 | Mock auth + role switcher; interceptor adds Bearer; header shows user and role | FRS §11.4; Brief §3.7.4 | `Web: UI_GEN_03_Interceptor_adds_bearer` | Planned |
| UI-GEN-04 | Material theme with a custom primary palette | FRS §11.4; Brief §3.7.4 | `Manual` | Planned |
| UI-GEN-05 | Errors → snackbars with severity (ProblemDetails aware) | FRS §11.4; Brief §3.7.4 | `Web: UI_GEN_05_Error_interceptor_shows_snackbar` | Planned |
| UI-GEN-06 | Loading states; buttons disabled while pending | FRS §11.4; Brief §3.7.4 | `Manual` | Planned |
| UI-GEN-07 | Reactive forms with Material error patterns | FRS §11.4; Brief §3.7.2 | covered by UI-FNOL tests | Planned |
| UI-GEN-08 | Usable at 1280px+; no console errors | FRS §11.4; Brief §6.2 | `Manual` | Planned |

---

## 13. Delivery (Brief §3.8, §4)

| ID | Requirement | Source | Evidence planned | Status |
|---|---|---|---|---|
| DEL-01 | README: prerequisites, local setup, config keys, migrations/seed, Azure deployment, walkthrough | Brief §4.2 | README.md | Planned |
| DEL-02 | ARCHITECTURE.md: structure, data model, CQRS flow, domain events, Hangfire, Azure, trade-offs | Brief §4.4 | ARCHITECTURE.md (from ARCHITECTURE-PLAN.md) | Planned |
| DEL-03 | AI-WORKFLOW.md: tools, workflow, ≥3 prompts, AI vs manual, ≥2 corrections, honest assessment | Brief §4.5 | AI-WORKFLOW.md + docs/ai-log | Planned |
| DEL-04 | AI interaction history export | Brief §4.6 | docs/ai-log exports | Planned |
| DEL-05 | Public API URL with Swagger, public UI URL, running at the review | Brief §3.8, §4.3 | Azure | Planned |
| DEL-06 | CI/CD: build backend, run EF migrations, build frontend, deploy both | Brief §3.8 | `.github/workflows/deploy.yml` | Planned |
| DEL-07 | docker-compose for local dev (SQL 2022 + Azurite) | Brief §4.1 | docker-compose.yml | Planned |
| DEL-08 | Seed scripts/migrations for codes, reference data, policies | Brief §4.1 | Migrations | Planned |
| DEL-09 | Test credentials: ≥1 handler, ≥1 supervisor (we provide 2 per role) | Brief §4.3; D-16 | README | Planned |
| DEL-10 | Azure resources: Container Apps (API, scale to zero; Brief §2.3 allows it), SWA, Azure SQL serverless, Storage, Key Vault | Brief §2.3, §3.8; D-36 | infra/ + README (incl. live-review runbook: minReplicas=1) | Planned |
