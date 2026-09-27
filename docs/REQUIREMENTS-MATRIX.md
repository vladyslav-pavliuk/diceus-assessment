# Requirements matrix — definition of done

One row per requirement. **Source**: `FRS §x` = `docs/spec/claims-frs.md`, `Brief §x` = `docs/spec/assessment-brief.md`,
`D-xx` = `docs/DECISIONS.md` (all ACCEPTED 2026-09-27).

**Enforced in** uses these layers: Domain (entity/aggregate invariant) · Validator (FluentValidation in the MediatR pipeline) ·
Handler (orchestration, lookups) · DB (constraint/index/trigger) · Infra (jobs, storage, auth) · API (endpoint, policy, middleware) · UI.

**Planned test** is named after the rule ID. Project prefixes: `Dom` = ClaimsModule.Domain.Tests, `App` = ClaimsModule.Application.Tests,
`Int` = ClaimsModule.IntegrationTests (WebApplicationFactory + Testcontainers MsSql), `Web` = claims-ui unit tests (Karma/Jest), `Manual` = checklist / smoke script.

**Status**: all rows are `Planned` at Phase 0. `Implemented (Pn)` = built and tested in phase n (the test column then names the real tests); `Partial (Pn)` = the part named in the row is built, the rest is planned. In Phase 8 each row becomes `Done` / `Partial` / `Missing`, with evidence (file:line + test).

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
| BR-C-01 | Loss date not in the future (Critical → 422 on POST) | FRS §7.1, §8; Brief §3.4.1; D-06, D-32 | Validator (TimeProvider), Domain (LossEvent ctor guard), UI | `Dom: BR_C_01_Future_loss_date_is_rejected`; `Dom: BR_C_01_Loss_date_equal_to_now_is_accepted`; `Dom: BR_C_01_Loss_date_is_stored_in_utc`; validator + HTTP: `App/Int: BR_C_01_*` (Phase 3) | Partial (P2) — domain guard; validator Phase 3 |
| BR-C-02 | Loss date outside the policy period → Warning issue; must be acknowledged/cleared before Open | FRS §7.1, §8, §5.4; Brief §3.4.1; D-19, D-32 | Domain (issue evaluation, Open guard), Handler (policy lookup), UI (in-force badge) | `Dom: BR_C_02_Loss_date_outside_policy_period_raises_warning`; `Dom: BR_C_02_Boundary_dates_are_inclusive`; `Dom: BR_C_02_The_utc_calendar_date_of_the_loss_is_compared`; `Dom: BR_C_02_Unacknowledged_warning_blocks_open`; `Dom: BR_C_02_Acknowledged_warning_allows_open`; `Int: BR_C_02_Expired_policy_POL_2023_000099_creates_draft_with_warning` (Phase 3) | Partial (P2) — domain; HTTP Phase 3 |
| BR-C-03 | ≥1 active Claimant before Draft → Open (Critical issue) | FRS §7.1, §4.2, §8; Brief §3.4.1; D-06 | Domain | `Dom: BR_C_03_Claim_without_claimant_is_created_with_critical_issue`; `Dom: BR_C_03_Open_without_active_claimant_is_rejected`; `Dom: BR_C_03_Adding_claimant_resolves_issue`; `Dom: BR_C_03_Adding_a_non_claimant_does_not_resolve_issue` | Implemented (P2) |
| BR-C-04 | Claim number unique per org, atomic, gap-free, `CLM-{YYYY}-{0000000}`; soft-deleted claims keep their numbers | FRS §7.1, §5.3; Brief §3.4.1; D-10 | Persistence (counter table in txn), DB (unique `(OrganisationId, ClaimNumber)`), Domain (ClaimNumber VO format) | `Dom: BR_C_04_Claim_number_format_is_CLM_year_7_digits`; `Dom: BR_C_04_Sequence_above_seven_digits_is_rejected`; `Dom: BR_C_04_Malformed_claim_numbers_do_not_parse`; `Int: BR_C_04_50_parallel_creates_yield_unique_gap_free_numbers` (mutation-checked); `Int: BR_C_04_Rolled_back_create_does_not_consume_number`; `Int: BR_C_04_Counter_is_per_year`; `Int: BR_C_04_Numbers_are_only_drawn_inside_a_transaction`; `Int: BR_C_04_Duplicate_claim_number_is_refused_by_the_database` | Implemented (P2) |
| BR-C-05 | CauseOfLossCode exists and is active for the org (Critical → 422) | FRS §7.1, §8; Brief §3.4.1; D-06, D-12 | Validator (async ref-data lookup), DB (FK) | `Dom: BR_C_05_Blank_cause_of_loss_code_is_rejected`; DB composite FK (OrganisationId, CauseOfLossCode); `App: BR_C_05_Unknown_cause_code_is_rejected`, `App: BR_C_05_Inactive_cause_code_is_rejected` (Phase 3) | Partial (P2) — FK + domain; validator Phase 3 |
| BR-C-06 | No policy → Warning "No policy linked…"; reserve creation blocked until linked | FRS §7.1, §8, §5.2; D-06 | Domain (reserve guard), Validator (initial reserve + null policy → 422) | `Dom: BR_C_06_Claim_without_policy_gets_warning`; `Dom: BR_C_06_Reserve_on_claim_without_policy_is_rejected`; `Dom: BR_C_06_Linking_policy_resolves_warning_and_unblocks_reserves`; `Dom: BR_C_06_Linking_an_expired_policy_raises_the_period_warning`; `App: BR_C_06_Initial_reserve_without_policy_is_rejected` (Phase 3) | Partial (P2) — domain; validator Phase 3 |
| BR-C-07 | Loss description required, ≥ 20 chars (Critical → 422) | FRS §7.1, §8 | Validator, Domain guard, UI (counter) | `Dom: BR_C_07_Description_shorter_than_20_chars_is_rejected`; `Dom: BR_C_07_Description_of_exactly_20_chars_is_accepted`; validator: `App: BR_C_07_*` (Phase 3) | Partial (P2) — domain guard; validator Phase 3 |
| B-C-06 | Draft ↛ Closed (brief path rule) | Brief §3.4.1; D-20 | Domain (transition table) | `Dom: B_C_06_Draft_to_closed_is_rejected` | Implemented (P2) |
| BR-R-01 | Amount > 0, except SubrogationRecoverable; Adjust = signed non-zero delta; non-Subrogation balance ≥ 0 | FRS §7.2, §8, §6.2, §9.6; Brief §3.4.2; D-05 | Validator (shape), Domain (balance rule) | `Dom: BR_R_01_Opening_a_cost_reserve_needs_a_positive_amount`; `Dom: BR_R_01_Missing_amount_is_rejected`; `Dom: BR_R_01_Negative_subrogation_is_accepted`; `Dom: BR_R_01_Zero_subrogation_is_rejected`; `Dom: BR_R_01_Subrogation_may_go_further_negative`; `Dom: BR_R_01_Adjust_below_zero_balance_is_rejected`; `Dom: BR_R_01_Adjust_down_to_exactly_zero_is_allowed`; `Dom: BR_R_01_Zero_adjustment_is_rejected`; `Dom: BR_R_01_Reverse_zeroes_component`; `Dom: BR_R_01_Reverse_rejects_a_client_amount_and_a_zero_balance`; validator shape tests Phase 4 | Partial (P2) — domain; validator Phase 4 |
| BR-R-02 | Authority tiers on \|txn amount\|: ≤10k auto; >10k–≤100k supervisor/manager; >100k manager; GL only after approval | FRS §7.2, §6.3; Brief §3.4.2 (BR-R-02/03/04), A.3; D-05 | Domain (`ReserveAuthorityPolicy`) | `Dom: BR_R_02_Tier_boundaries` (10,000 / 10,000.01 / 100,000 / 100,000.01, ±); `Dom: BR_R_02_Who_can_approve_each_tier`; `Dom: BR_R_02_Amount_10000_is_auto_approved`; `Dom: BR_R_02_Amount_10000_01_requires_supervisor`; `Dom: BR_R_02_Amount_100000_requires_supervisor`; `Dom: BR_R_02_Amount_100000_01_requires_manager`; `Dom: BR_R_02_Negative_amount_uses_absolute_value`; `Dom: BR_R_02_Supervisor_cannot_approve_above_100000`; `Dom: BR_R_02_Supervisor_can_approve_up_to_100000`; `Dom: BR_R_02_Manager_can_approve_above_100000`; `Dom: BR_R_02_Handler_cannot_approve_a_supervisor_tier_transaction`; `Int: BR_R_02_Pending_reserve_enqueues_no_gl_job` (Phase 4) | Partial (P2) — domain; GL enqueue Phase 4 |
| BR-R-03 | No self-approval → 422 "Self-approval is not permitted." | FRS §7.2, §6.3, §8; D-25 | Domain | `Dom: BR_R_03_Self_approval_is_rejected`; `Dom: BR_R_03_Another_user_with_the_same_role_can_approve`; `Dom: BR_R_03_Supervisor_self_approving_above_100000_gets_both_reasons`; `Int: BR_R_03_Self_approval_returns_422_with_exact_message` (Phase 4) | Partial (P2) — domain; HTTP Phase 4 |
| BR-R-04 | Rejected txn stays in history (Rejected); resubmission creates a new record | FRS §7.2, §6.4; Brief BR-R-06 | Domain | `Dom: BR_R_04_Rejected_txn_is_retained_and_resubmission_creates_new_txn` | Implemented (P2) |
| BR-R-05 | Approved total ≤ $10M unless the Manager override is set; Warning at submit, blocked at approval | FRS §7.2, §8, §3; Brief BR-R-07, A.3; D-11 | Domain, API (Manager policy on override) | `Dom: BR_R_05_Exactly_10M_is_allowed`; `Dom: BR_R_05_Crossing_10M_without_override_warns_and_escalates_to_manager`; `Dom: BR_R_05_Approval_blocked_until_override_set`; `Dom: BR_R_05_Override_set_before_submission_keeps_the_normal_tier`; `Dom: BR_R_05_Limit_is_rechecked_at_approval`; `Dom: BR_R_05_Subrogation_excluded_from_aggregate`; `Dom: BR_R_05_A_decrease_is_never_blocked_by_the_limit`; `Dom: BR_R_05_Only_a_manager_sets_the_override_and_a_reason_is_required`; `Int: BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit` (Phase 4; mechanism proven by `Int: CONV_08_Concurrent_changes_to_one_claim_conflict_on_RowVer`) | Partial (P2) — domain; API Phase 4 |
| BR-R-06 | GL idempotency key `Reserve:{ComponentId}:Change:{Seq}`; re-entrant, no duplicate audit | FRS §7.2, §6.5, §12.1; Brief BR-R-05; D-23 | Domain (key), DB (unique filtered index), Infra (job) | `Dom: BR_R_06_Idempotency_key_format`; DB `UX_ReserveHistory_IdempotencyKey`; `Int: BR_R_06_Job_run_twice_writes_one_gl_audit_entry`, `Int: BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry` (Phase 4) | Partial (P2) — key + index; job Phase 4 |
| BR-ST-01 | Only §4.2 transitions; invalid → 422 listing the valid next statuses | FRS §7.3, §4.2, §8; D-09 | Domain (transition table) | `Dom: BR_ST_01_Invalid_transition_lists_valid_next_statuses`; `Dom: BR_ST_01_A_terminal_status_lists_no_valid_next_status`; `Int: BR_ST_01_Invalid_transition_returns_422` (Phase 3) | Partial (P2) — domain; HTTP Phase 3 |
| BR-ST-02 | → Open requires no Critical issues (and BR-C-02 acknowledged) + ≥1 Claimant (+ handler set, D-18) | FRS §7.3, §4.1, §4.2; D-18, D-19 | Domain | `Dom: BR_ST_02_Open_with_unresolved_critical_is_rejected_listing_every_condition`; `Dom: BR_ST_02_Open_with_all_conditions_met_succeeds_despite_non_blocking_warnings`; handler check unreachable by design (D-39 item 17) | Implemented (P2) |
| BR-ST-03 | → Closed requires CC-01..04; 422 listing every blocking condition | FRS §7.3, §4.3 | Domain | `Dom: BR_ST_03_Close_lists_all_failed_conditions` | Implemented (P2) |
| BR-ST-04 | Reopen: Supervisor+ and a non-empty reason; immediately → Open | FRS §7.3, §4.2; D-09, D-26 | API (policy), Domain | `Dom: BR_ST_04_Reopen_moves_claim_to_open` (event order: STATUS_CHANGED, CLAIM_REOPENED, STATUS_CHANGED); `Dom: BR_ST_04_Reopen_without_reason_is_rejected`; `Dom: BR_ST_04_Handler_cannot_reopen`; `Dom: BR_ST_04_Manager_can_reopen_because_roles_are_hierarchical`; `Int: BR_ST_04_Handler_reopen_returns_403`, `Int: BR_ST_04_Reopen_writes_three_audit_rows` (Phase 3) | Partial (P2) — domain; HTTP + audit Phase 3 |
| CC-01 | No PendingApproval reserve txns at closure | FRS §4.3; D-21 | Domain | `Dom: CC_01_Close_with_pending_reserve_is_rejected` | Implemented (P2) |
| CC-02 | No unresolved Critical issues at closure | FRS §4.3; D-07 | Domain | `Dom: CC_02_Close_with_open_critical_issue_is_rejected` (configured Draft → Closed row, D-39 item 17) | Implemented (P2) |
| CC-03 | ≥1 active Claimant at closure | FRS §4.3 | Domain | `Dom: CC_03_Close_without_active_claimant_is_rejected` (configured Draft → Closed row, D-39 item 17) | Implemented (P2) |
| CC-04 | CurrentAmount > 0 → warning; closure requires a justification note | FRS §4.3; D-26 | Domain, UI (pre-flight checklist) | `Dom: CC_04_Close_with_open_reserves_without_justification_is_rejected`; `Dom: CC_04_Close_with_open_reserves_and_justification_succeeds`; `Dom: CC_04_Zero_and_negative_balances_are_not_open_reserves` | Implemented (P2) |
| BR-D-01 | Blob path `{org}/{claim}/{…filename}` in `claim-documents`; filename sanitised | FRS §7.4, §13; Brief §3.6; D-28 | Infra (storage), Domain (FileName VO) | `Dom: BR_D_01_Path_traversal_is_stripped`; `Dom: BR_D_01_Control_reserved_and_device_names_are_neutralised`; `Dom: BR_D_01_Unicode_is_normalised_to_NFC`; `Dom: BR_D_01_Names_that_are_empty_after_sanitising_are_rejected`; `Dom: BR_D_01_Long_names_are_capped_and_keep_their_extension`; `Int: BR_D_01_Blob_is_stored_under_org_and_claim_prefix` (Phase 5) | Partial (P2) — file-name VO; storage Phase 5 |
| BR-D-02 | Retrieval = SAS URL, 1h TTL; bytes never proxied (Azure) | FRS §7.4, §13; Brief §3.6 | Infra | `Int: BR_D_02_Sas_url_expires_in_one_hour`; `Int: BR_D_02_List_documents_returns_sas_not_bytes` | Planned |
| BR-D-03 | Fallback to local FS via `IStorageService`; provider chosen in appsettings | FRS §7.4, §13; Brief §3.6; D-28 | Infra, API (DI) | `Int: BR_D_03_LocalFileSystem_provider_round_trip`; `Int: BR_D_03_Provider_selected_from_config` | Planned |
| BR-P-01 | Zero or more parties; ≥1 Claimant before Open (Critical) | FRS §7.5 | Domain | covered by BR-C-03 tests + `Dom: BR_P_01_Claim_can_exist_with_zero_parties` | Implemented (P2) |
| BR-P-02 | Roles Claimant/Insured/ThirdParty/Witness/Attorney; duplicates allowed | FRS §7.5, §9.3 | Validator (enum), Domain | `Dom: BR_P_02_Multiple_claimants_are_allowed`; `Dom: BR_P_02_Every_party_role_is_supported`; `Dom: BR_P_02_Unknown_party_role_is_rejected`; `App: BR_P_02_Unknown_party_role_is_rejected` (Phase 3) | Partial (P2) — domain; validator Phase 3 |
| PTY-01 | Removing the last active Claimant → 422 (soft-remove via IsActive) | FRS §10.1, §11.3; D-27 | Domain, UI (disabled button) | `Dom: PTY_01_Removing_last_active_claimant_is_rejected`; `Dom: PTY_01_A_claimant_can_be_removed_while_another_is_active`; `Dom: PTY_01_Removed_party_is_inactive_not_deleted`; `Dom: PTY_01_Removing_an_already_removed_party_is_rejected`; `Dom: PTY_01_Removing_an_unknown_party_is_not_found` | Implemented (P2) |
| BR-A-01 | Audit log append-only; no UPDATE/DELETE | FRS §7.6, §14.2; D-14 | Persistence (interceptor), DB (trigger/DENY) | `Int: BR_A_01_Modifying_or_deleting_an_audit_entry_throws`; `Int: BR_A_01_Db_trigger_blocks_raw_update_and_delete` (trigger error 51000; also stops set-based ExecuteUpdate); `Int: CONV_05_D_14_The_audit_log_has_no_update_or_delete_columns` | Implemented (P2) |
| BR-A-02 | Every significant action writes an audit entry | FRS §7.6, §14.1 | Application (before-commit handlers) | see AUD-01..16 | Planned |

---

## 3. Status transitions (FRS §4.2) — seeded `ClaimStatusTransitions` (D-09)

| ID | Transition | Conditions | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|---|
| TR-00 | Any unlisted transition → 422 with valid next statuses | — | FRS §4.2, BR-ST-01 | Domain | `Dom: TR_00_Unlisted_transition_is_rejected` (theory over every unlisted pair from each resting status); `Dom: D_09_Transition_table_matches_FRS_4_2` | Implemented (P2) |
| TR-01 | Draft → Open | BR-ST-02, D-18, D-19 | FRS §4.2 | Domain | `Dom: TR_01_Draft_to_open` | Implemented (P2) |
| TR-02 | Open → UnderInvestigation | none | FRS §4.2 | Domain | `Dom: TR_02_Open_to_under_investigation` | Implemented (P2) |
| TR-03 | Open → PendingPayment | ≥1 approved reserve txn | FRS §4.2; D-21 | Domain | `Dom: TR_03_TR_07_Pending_payment_requires_an_approved_reserve` | Implemented (P2) |
| TR-04 | Open → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_04_TR_08_TR_10_Close_records_reason_and_time`; `Dom: TR_04_Close_without_reason_is_rejected` | Implemented (P2) |
| TR-05 | Open → Withdrawn | reason required; no pending txns (D-26) | FRS §4.2 | Domain | `Dom: TR_05_TR_09_Withdrawal_requires_a_reason`; `Dom: TR_05_Withdrawal_is_blocked_while_a_reserve_is_pending` | Implemented (P2) |
| TR-06 | UnderInvestigation → Open | none | FRS §4.2 | Domain | `Dom: TR_06_Under_investigation_to_open` | Implemented (P2) |
| TR-07 | UnderInvestigation → PendingPayment | ≥1 approved reserve txn | FRS §4.2 | Domain | `Dom: TR_03_TR_07_Pending_payment_requires_an_approved_reserve` | Implemented (P2) |
| TR-08 | UnderInvestigation → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_04_TR_08_TR_10_Close_records_reason_and_time` | Implemented (P2) |
| TR-09 | UnderInvestigation → Withdrawn | reason required | FRS §4.2 | Domain | `Dom: TR_05_TR_09_Withdrawal_requires_a_reason` | Implemented (P2) |
| TR-10 | PendingPayment → Closed | CC-01..04 | FRS §4.2 | Domain | `Dom: TR_04_TR_08_TR_10_Close_records_reason_and_time` | Implemented (P2) |
| TR-11 | Closed → Reopened | supervisor+, reason | FRS §4.2, BR-ST-04 | Domain, API | see BR-ST-04 | Partial (P2) — domain; API policy Phase 3 |
| TR-12 | Reopened → Open | automatic, same command | FRS §4.2, BR-ST-04 | Domain | `Dom: TR_12_Reopened_to_open_is_system_only`; `Dom: TR_12_Reopen_fails_safely_when_the_automatic_row_is_missing` | Implemented (P2) |
| TR-13 | Closed/Withdrawn claims are read-only (reserves, parties, docs, policy, notes) | NOT SPECIFIED → D-26 | D-26 | Domain | `Dom: TR_13_Changes_on_closed_and_withdrawn_claims_are_rejected` (theory over 9 write actions × Closed/Withdrawn) | Implemented (P2) |

---

## 4. Validation messages (FRS §8) — exact wording asserted in tests

| ID | Field | Rule | Severity | Message (verbatim) | Enforced in | Planned test | Status |
|---|---|---|---|---|---|---|---|
| VAL-01 | LossDate | not in the future | Critical | Loss date cannot be in the future. | Validator | `App: VAL_01_message` | Planned |
| VAL-02 | LossDate | valid/required | Critical | Loss date is required. | Validator | `App: VAL_02_message` | Planned |
| VAL-03 | LossDate | within policy period | Warning | Loss date is outside the policy effective period. | Domain (issue) | asserted verbatim in `Dom: BR_C_02_Loss_date_outside_policy_period_raises_warning` | Implemented (P2) |
| VAL-04 | LossDescription | required, ≥20 | Critical | Loss description is required and must be at least 20 characters. | Validator | `App: VAL_04_message` | Planned |
| VAL-05 | CauseOfLossCode | exists & active | Critical | Cause of loss code is not recognised or is inactive. | Validator | `App: VAL_05_message` | Planned |
| VAL-06 | PolicyId | null → warning | Warning | No policy linked. Policy must be associated before reserves can be set. | Domain (issue) | asserted verbatim in `Dom: BR_C_06_Claim_without_policy_gets_warning` | Implemented (P2) |
| VAL-07 | ClaimParties | ≥1 Claimant before Open | Critical | At least one Claimant party is required to open a claim. | Domain | asserted verbatim in `Dom: BR_C_03_Claim_without_claimant_is_created_with_critical_issue` | Implemented (P2) |
| VAL-08 | ReserveAmount | > 0 unless Subrogation | Critical | Reserve amount must be greater than zero. | Validator | `App: VAL_08_message` | Planned |
| VAL-09 | ReserveComponent | defined type | Critical | Invalid reserve component type. | Validator | `App: VAL_09_message` | Planned |
| VAL-10 | ReserveAmount | aggregate ≤ $10M | Warning | Total reserves will exceed $10,000,000. Manager override required. | Domain | asserted verbatim in `Dom: BR_R_05_Crossing_10M_without_override_warns_and_escalates_to_manager` | Implemented (P2) |
| VAL-11 | StatusTransition | valid next status | Critical | Transition from {from} to {to} is not permitted. | Domain | asserted verbatim in `Dom: TR_00_Unlisted_transition_is_rejected` | Implemented (P2) |
| VAL-12 | StatusTransition | closure conditions | Critical | Claim cannot be closed — {condition} is not satisfied. | Domain | asserted verbatim in `Dom: CC_01_Close_with_pending_reserve_is_rejected` (condition text carries the CC id) | Implemented (P2) |
| VAL-13 | ReserveApproval | role authority | Critical | Your role does not have authority to approve this reserve amount. | Domain | asserted verbatim in `Dom: BR_R_02_Handler_cannot_approve_a_supervisor_tier_transaction` | Implemented (P2) |
| VAL-14 | ReserveApproval | not submitter | Critical | Self-approval is not permitted. | Domain | asserted verbatim in `Dom: BR_R_03_Self_approval_is_rejected` | Implemented (P2) |

Note: FRS §7.1 BR-C-06 words the no-policy warning differently ("No policy linked — claim requires policy association before financial actions are
permitted."). The §8 table's wording (VAL-06) is used because §8 is the consolidated message table. Recorded under D-02.

---

## 5. Reserve workflow (FRS §6) beyond the BR-R rules

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| RSV-01 | Components: Indemnity/Expense/ALAE/SubrogationRecoverable; several per claim | FRS §6.2; D-03 | Domain, Validator | `Dom: RSV_01_Claim_can_hold_multiple_components` | Implemented (P2) |
| RSV-02 | PendingApproval txn is immutable; submitter may retract → Cancelled | FRS §6.4 rule box; §10.2 | Domain | `Dom: RSV_02_Pending_transaction_cannot_be_modified`; `Dom: RSV_02_After_retraction_a_new_transaction_can_be_submitted`; `Dom: RSV_02_Retract_by_submitter_cancels_txn`; `Dom: RSV_02_Retract_by_other_user_is_rejected`; `Dom: RSV_02_Retract_non_pending_is_rejected` | Implemented (P2) |
| RSV-03 | Approve → Approved + GL enqueued (after commit); Reject → Rejected + reason | FRS §6.4 steps 8–9 | Domain, Application (after-commit) | `Dom: RSV_03_Approve_sets_approved_and_raises_event`; `Dom: RSV_03_Reject_requires_reason`; `Dom: RSV_03_Reject_needs_the_same_authority_as_approve`; `Dom: RSV_03_Only_a_pending_transaction_can_be_decided`; `Dom: RSV_03_Unknown_transaction_is_not_found`; `Int: RSV_03_Approval_enqueues_gl_job_after_commit` (Phase 4) | Partial (P2) — domain; enqueue Phase 4 |
| RSV-04 | Event-sourced: INSERT history rows; CurrentAmount = Σ approved; never UPDATE amounts | FRS §6.6, §9.5, §9.6; D-11, D-22 | Domain, Persistence (interceptor guards amount columns) | `Dom: RSV_04_Current_amount_is_sum_of_approved_txns`; `Int: RSV_04_Modifying_a_history_amount_throws` | Implemented (P2) |
| RSV-05 | ChangeSequence monotonic per component, protected by RowVer | FRS §9.6, §15.1; D-23 | Domain, DB (RowVer) | `Dom: RSV_05_Change_sequence_increments_per_component_and_is_never_reused`; DB `UX_ReserveHistory_ReserveComponentId_ChangeSequence`; `Int: RSV_05_Concurrent_submissions_on_same_component_one_gets_409` (Phase 4) | Partial (P2) — domain + index; HTTP 409 Phase 4 |
| RSV-06 | At most one PendingApproval per component | D-22 (ASSUMPTION) | Domain, DB (unique filtered index) | `Dom: RSV_02_Pending_transaction_cannot_be_modified`; `Dom: RSV_06_Even_an_auto_approvable_change_waits_for_the_pending_one`; `Dom: RSV_06_A_pending_transaction_does_not_block_other_components`; `Int: RSV_06_Database_allows_one_pending_transaction_per_component` | Implemented (P2) |
| RSV-07 | Concurrent approvals of the same txn → exactly one succeeds (409 for the other) | FRS §15.1 RowVer; PROMPTS Phase 4 | Domain + DB (RowVer on Claim/component) | `Int: RSV_07_Concurrent_approvals_one_wins` | Planned |
| RSV-08 | History stores previous/new balance, reason, submitter, approver, timestamps, posting status | FRS §6.6, §9.6 | Domain | `Dom: RSV_08_History_row_captures_balances_and_actors`; `Dom: RSV_08_Submission_raises_an_event_describing_the_transaction` | Implemented (P2) |
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
| API-28 | `POST /api/auth/dev-token`, `GET /api/auth/users` | Signed JWT for a seeded user; config-gated | FRS §3, §11.4; D-16, D-38 | Infra (token), Application (user queries), API | `Int: API_28_Dev_token_is_a_valid_bearer_token`; `Int: API_28_Dev_token_carries_sub_name_role_and_org_claims_and_expires_after_8_hours`; `Int: API_28_Unknown_user_gets_401`; `Int: API_28_Endpoints_are_absent_when_dev_tokens_are_disabled`; `App: API_28_Missing_username_is_rejected` | Implemented (P1) |
| API-29 | `GET /api/users?role=` | Handler list for filter/assign | FRS §11.1; D-29 | Application | `Int: API_29_List_handlers` | Planned |
| API-ERR | Errors: ProblemDetails; 422 `{type,title,status,errors{field:[msgs]}}`; 404/403/409 mapping | FRS §10.4; CLAUDE.md rule 12; D-38 | API middleware | `Int: API_ERR_422_body_matches_FRS_10_4_exactly`; `Int: API_ERR_Business_rule_violation_gets_422_with_the_same_shape`; `Int: API_ERR_Unbindable_body_gets_422_with_the_same_shape`; `Int: API_ERR_Exceptions_map_to_status_codes` (404/403/409); `Int: API_ERR_Unknown_route_gets_404_problem_for_a_signed_in_caller`; `Int: API_ERR_Unhandled_exception_gets_500_without_internal_details_outside_development` | Implemented (P1) |
| API-IDEMP | `Idempotency-Key` on writes replays the stored response | FRS §10; D-24 | API filter, DB | `Int: API_IDEMP_Repeated_key_replays_response`; `Int: API_IDEMP_Same_key_different_body_returns_422` | Planned |
| API-AUTH | Bearer JWT on all endpoints except the dev token, Swagger and health | FRS §10; Brief §3.7.4 | API (fallback policy) | `Int: API_AUTH_Missing_token_gets_401`; `Int: API_AUTH_Tampered_token_gets_401`; `Int: API_AUTH_Token_signed_with_another_key_gets_401`; `Int: API_AUTH_Expired_token_gets_401`; `Int: API_AUTH_Anonymous_endpoints_do_not_need_a_token`; `Int: API_AUTH_Unknown_route_gets_401_for_an_anonymous_caller` | Implemented (P1) |
| API-CORR | `X-Correlation-Id` read or created; echoed; stamped on audit rows | FRS §14.2; CLAUDE.md rule 6; D-38 | API middleware | `Int: API_CORR_Incoming_correlation_id_is_used_and_echoed`; `Int: API_CORR_Missing_correlation_id_is_generated`; `Int: API_CORR_Non_guid_correlation_id_is_replaced` (GUID only, D-39 Q1); `Int: API_CORR_Error_responses_carry_the_correlation_id`; audit stamping at the persistence level: `Int: AUD_I3_Audit_rows_carry_tenant_correlation_id_actor_and_utc_time`; over HTTP: `Int: API_CORR_Correlation_id_is_stamped_on_audit_rows` (Phase 3) | Partial (P2) |
| API-DOCS | Swagger/OpenAPI populated, with a JWT scheme, reachable when deployed | Brief §4.3, §6.2 | API | `Int: API_DOCS_OpenAPI_document_declares_the_JWT_bearer_scheme`; `Manual: Swagger reachable on Azure URL` (Phase 7) | Partial (P1) |
| API-LOG | Structured logging (Serilog; JSON outside development) with the correlation id in scope; MediatR LoggingBehavior logs request name, outcome, duration | Brief §6.2; CLAUDE.md rule 12 | API, Application | `Manual:` log line `[.. INF] <correlation-id> ...LoggingBehavior: Handled GetUserByUsernameQuery in … ms` (compose smoke, Phase 1) | Implemented (P1) |
| OPS-01 | Health: `/health/live` (no dependencies) and `/health/ready` (database), anonymous | Container Apps probes; D-36, D-38 | API, Persistence | `Int: OPS_01_Health_endpoints_report_healthy_without_a_token` | Implemented (P1) |
| OPS-02 | CORS for the SPA origin only (config `Cors:AllowedOrigins`); exposes X-Correlation-Id and Location | ARCHITECTURE-PLAN §7 | API | `Int: OPS_02_Preflight_from_the_SPA_origin_is_allowed`; `Int: OPS_02_Preflight_from_another_origin_is_not_allowed`; `Int: OPS_02_Correlation_and_location_headers_are_exposed_to_the_SPA` | Implemented (P1) |

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
| AUD-02 | STATUS_CHANGED | every transition; Old/New = statuses | FRS §14.1 | `Dom: AUD_02_Status_change_raises_event_with_old_and_new_status`; audit row Phase 3 | Partial (P2) — event; audit row Phase 3 |
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
| AUD-I2 | Timestamps DATETIMEOFFSET(7) UTC | FRS §14.2 | `Int: AUD_I3_Audit_rows_carry_tenant_correlation_id_actor_and_utc_time` | Implemented (P2) |
| AUD-I3 | Request CorrelationId on all audit rows of a request | FRS §14.2 | `Int: AUD_I3_Audit_rows_carry_tenant_correlation_id_actor_and_utc_time` (persistence); HTTP Phase 3 | Partial (P2) |
| AUD-I4 | Audit write is in the same transaction as the state change | CLAUDE.md rule 5 | `Int: AUD_I4_Failed_command_leaves_no_audit_row` | Planned |

---

## 10. Roles & security (FRS §3, Brief §3.7.4)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| SEC-01 | Three roles handler/supervisor/manager, hierarchical | FRS §3 | Domain (`UserRole.IsAtLeast`), Infra (JWT), API policies | `Dom: SEC_01_Roles_are_hierarchical_handler_supervisor_manager`; `Dom: SEC_01_Role_codes_match_FRS_section_3`; `Int: SEC_01_Role_policies_are_hierarchical` | Implemented (P1) |
| SEC-02 | Backend validates the caller's role on approval endpoints | FRS §3 | API policy + Domain | policy mechanism: `Int: SEC_02_Forbidden_role_gets_403_problem`; on the real endpoint: `Int: SEC_02_Handler_approve_returns_403` (Phase 4) | Partial (P1) |
| SEC-03 | Approve/Reject visible only to supervisor/manager | FRS §3, §11.3; Brief §3.7.4 | UI | `Web: SEC_03_Approve_buttons_hidden_for_handler` | Planned |
| SEC-04 | Tenant isolation: data from another org is invisible | FRS §15.1; D-12 | Persistence (global filter) | `Int: SEC_04_Other_tenants_rows_are_invisible_and_cannot_be_written`; `Int: SEC_04_Without_a_tenant_nothing_is_visible_and_nothing_can_be_inserted`; HTTP `Int: SEC_04_Cross_tenant_claim_is_not_found` (Phase 3) | Partial (P2) — persistence; HTTP Phase 3 |
| SEC-05 | Test users: 2 per role | Brief §4.3; D-16 | Seed (HasData) | `Int: SEC_05_Two_seeded_users_per_role_in_one_organisation` | Implemented (P1) |

---

## 11. Data conventions (FRS §15, Brief §3.2, A.4)

| ID | Requirement | Source | Enforced in | Planned test | Status |
|---|---|---|---|---|---|
| CONV-01 | GUID PKs, default `NEWSEQUENTIALID()` (domain-assigned sequential values, D-30) | FRS §15.1, §15.2 | Persistence config | `Int: CONV_01_All_guid_primary_keys_default_to_NEWSEQUENTIALID`; `Dom: D_30_Sequential_guids_increase_in_SQL_Server_order`; `Dom: D_30_Entities_get_their_id_at_construction` | Implemented (P2) |
| CONV-02 | Money DECIMAL(19,4) via `HasPrecision(19,4)`; no float/money | FRS §15.1, §15.2 | Persistence config | `Int: CONV_02_All_decimals_are_19_4`; `Dom: FRS_15_1_Amounts_with_more_than_four_decimals_are_rejected` | Implemented (P2) |
| CONV-03 | Timestamps DATETIMEOFFSET(7), UTC | FRS §15.1 | Persistence config | `Int: CONV_03_All_timestamps_are_datetimeoffset_7`; `Dom: BR_C_01_Loss_date_is_stored_in_utc`; `Int: AUD_I3_Audit_rows_carry_tenant_correlation_id_actor_and_utc_time` | Implemented (P2) |
| CONV-04 | Short codes NVARCHAR(50); names NVARCHAR(255); free text NVARCHAR(MAX); enums as NVARCHAR(50) | FRS §15.1; CLAUDE.md rule 10 | Persistence config | `Int: CONV_04_Enums_are_stored_as_nvarchar_text` | Implemented (P2) |
| CONV-05 | Soft delete (IsDeleted + DeletedAt) + global filter on business tables (not the audit log, D-14) | FRS §15.1, §15.2 | Persistence | `Int: CONV_05_Every_business_table_has_soft_delete_audit_and_tenant_columns`; `Int: CONV_05_Deleted_rows_are_soft_deleted_and_filtered`; `Int: CONV_05_D_14_The_audit_log_has_no_update_or_delete_columns` | Implemented (P2) |
| CONV-06 | Audit columns filled by an interceptor | FRS §15.1 | Persistence interceptor | `Int: CONV_06_Audit_columns_are_populated_and_a_child_change_touches_the_claim` | Implemented (P2) |
| CONV-07 | OrganisationId on every business table + global filter | FRS §15.1; D-12 | Persistence | see SEC-04; `Int: CONV_05_Every_business_table_has_soft_delete_audit_and_tenant_columns` | Implemented (P2) |
| CONV-08 | RowVer on Claims and ClaimReserveComponents | FRS §15.1 | Persistence | `Int: CONV_08_RowVer_is_on_the_aggregate_roots`; `Int: CONV_08_Concurrent_changes_to_one_claim_conflict_on_RowVer` | Implemented (P2) |
| CONV-09 | Fluent API only (`IEntityTypeConfiguration<T>`), no schema annotations | FRS §15.2 | Persistence | `Int: CONV_09_Domain_uses_no_data_annotations` | Implemented (P1) |
| CONV-10 | Seed via HasData/migrations (policies §5.5, codes §5.6, transitions, users, org) — never at startup | FRS §15.4, §5.5, §5.6; Brief §4.1 | Persistence | `Int: CONV_10_Policies_match_FRS_5_5`; `Int: CONV_10_Cause_of_loss_codes_match_FRS_5_6`; `Int: CONV_10_Status_transitions_are_the_domain_table`; `Int: CONV_10_The_organisation_and_its_users_are_seeded` | Implemented (P2) |
| CONV-11 | `dotnet ef database update` builds a fresh DB from zero; app starts with no manual setup | FRS §15.4 | Persistence | `Int: CONV_11_Migrations_build_the_database_from_zero`; `Int: CONV_11_Model_has_no_changes_missing_from_the_migrations`; `Manual:` compose `migrate` (EF bundle) then `api` start (Phase 1) | Implemented (P1) — re-verified each phase |
| CONV-12 | No hard-coded GUIDs in application logic | FRS §15.4 | Code review + grep | `Manual: CONV_12_grep_for_guid_literals_outside_seed` | Planned |
| CONV-13 | Naming: tables/columns PascalCase; VerbNounCommand; Get/List…Query; kebab-case routes | FRS §15.3 | Architecture test | `Int: CONV_13_Commands_are_named_VerbNounCommand`; `Int: CONV_13_Queries_are_named_GetNounQuery_or_ListNounsQuery`; `Int: CONV_13_Every_request_is_a_command_or_a_query` | Implemented (P1) — tables/routes by review |
| CONV-14 | Clean Architecture dependency rule | Brief §2.4; CLAUDE.md | Project references + architecture tests | `Int: CONV_14_Domain_depends_on_no_other_layer_or_framework`; `Int: CONV_14_Application_does_not_depend_on_outer_layers_EF_Core_or_Azure`; `Int: CONV_14_Infrastructure_does_not_depend_on_Persistence_or_API`; `Int: CONV_14_Persistence_does_not_depend_on_Infrastructure_or_API`; `Int: CONV_14_Controllers_stay_thin_and_never_touch_persistence` | Implemented (P1) |
| CONV-15 | FluentValidation in the MediatR pipeline; AutoMapper profiles in Application; UoW coordinates each command | Brief §2.4, §6.1; D-37 | Application | `App: CONV_15_Validation_behavior_short_circuits_handler`; `App: CONV_15_Errors_from_all_validators_are_grouped_by_property`; `App: CONV_15_AutoMapper_configuration_is_valid`; `App: CONV_15_Mapped_types_are_not_self_referencing`; `App: CONV_15_Requests_are_never_mapped`; `Int: CONV_15_AutoMapper_profiles_live_only_in_Application`; UoW: Phase 2 | Partial (P2) — UnitOfWork exists (execution strategy + transaction); UnitOfWorkBehavior and event dispatch Phase 3 |
| DOM-01 | Entities protect invariants: no public/protected setters; child collections read-only | CLAUDE.md rule 3 | Domain | `Dom: DOM_01_Entities_have_no_public_setters`; `Dom: DOM_01_Aggregate_collections_are_read_only` | Implemented (P2) |
| DOM-02 | The whole Claim aggregate (incl. every reserve transaction) round-trips through EF and stays usable (D-39 Q2) | D-39 | Persistence (IClaimRepository) | `Int: DOM_02_Full_aggregate_round_trips_and_stays_usable` | Implemented (P2) |
| CONV-16 | Time only through an injected `TimeProvider`; no `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` in production code | CLAUDE.md rule 9 | Architecture test (IL scan) | `Int: CONV_16_Production_code_never_reads_the_system_clock_directly` | Implemented (P1) |

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
| DEL-07 | docker-compose for local dev (SQL 2022 + Azurite) | Brief §4.1 | `docker-compose.yml` (+ `app` profile: EF migrations bundle + API from the production Dockerfile); smoke-tested in Phase 1 | Implemented (P1) |
| DEL-08 | Seed scripts/migrations for codes, reference data, policies | Brief §4.1 | Migrations | Planned |
| DEL-09 | Test credentials: ≥1 handler, ≥1 supervisor (we provide 2 per role) | Brief §4.3; D-16 | README | Planned |
| DEL-10 | Azure resources: Container Apps (API, scale to zero; Brief §2.3 allows it), SWA, Azure SQL serverless, Storage, Key Vault | Brief §2.3, §3.8; D-36 | infra/ + README (incl. live-review runbook: minReplicas=1) | Planned |
