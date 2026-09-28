import { ReserveHistoryEntry } from '../../core/models/reserve.models';
import { reserveRowActions } from './reserve-actions';
import {
  balanceError,
  effectiveAmount,
  inferTransactionType,
  submissionBlocker,
} from './reserve-submission';

function row(overrides: Partial<ReserveHistoryEntry> = {}): ReserveHistoryEntry {
  return {
    id: 't1',
    component: 'Indemnity',
    transactionType: 'Add',
    amount: 25_000,
    approvalStatus: 'PendingApproval',
    requiredAuthority: 'Supervisor',
    postingStatus: 'Pending',
    submittedByUserId: 'handler-alex',
    ...overrides,
  } as ReserveHistoryEntry;
}

const handler = { id: 'handler-alex', role: 'Handler' as const };
const supervisor = { id: 'supervisor-casey', role: 'Supervisor' as const };
const manager = { id: 'manager-emery', role: 'Manager' as const };

describe('SEC-03 / UI-DET-08 approve and reject are role-gated', () => {
  it('SEC_03_Approve_buttons_hidden_for_handler', () => {
    expect(reserveRowActions(row(), handler, false).showDecision).toBe(false);
  });

  it('SEC_03_Approve_buttons_shown_to_supervisor_and_manager on pending rows only', () => {
    expect(reserveRowActions(row(), supervisor, false).showDecision).toBe(true);
    expect(reserveRowActions(row(), manager, false).showDecision).toBe(true);
    expect(
      reserveRowActions(row({ approvalStatus: 'Approved' }), manager, false).showDecision,
    ).toBe(false);
  });

  it('BR_R_03_Self_approval_is_disabled_with_the_API_message', () => {
    const own = row({ submittedByUserId: supervisor.id });
    expect(reserveRowActions(own, supervisor, false).approveBlockedReason).toBe(
      'Self-approval is not permitted.',
    );
  });

  it('D_45_Self_rejection_is_disabled_and_Retract_is_offered', () => {
    const own = row({ submittedByUserId: supervisor.id });
    const actions = reserveRowActions(own, supervisor, false);
    expect(actions.rejectBlockedReason).toBe(
      'Self-rejection is not permitted. Use Retract to withdraw your own pending reserve.',
    );
    expect(actions.showRetract).toBe(true);
  });

  it('BR_R_02_Supervisor_cannot_approve_a_manager_tier_transaction', () => {
    const large = row({ amount: 250_000, requiredAuthority: 'Manager' });
    const actions = reserveRowActions(large, supervisor, false);
    expect(actions.approveBlockedReason).toBe(
      'Your role does not have authority to approve this reserve amount.',
    );
    expect(actions.rejectBlockedReason).toBe(
      'Your role does not have authority to reject this reserve amount.',
    );
    expect(reserveRowActions(large, manager, false).approveBlockedReason).toBeNull();
  });

  it('D_26_No_decisions_on_a_read_only_claim', () => {
    expect(reserveRowActions(row(), manager, true).showDecision).toBe(false);
  });
});

describe('UI-DET-09 retract and UI-DET-10 GL retry', () => {
  it('UI_DET_09_Retract_only_for_submitter', () => {
    expect(reserveRowActions(row(), handler, false).showRetract).toBe(true);
    expect(reserveRowActions(row(), supervisor, false).showRetract).toBe(false);
    expect(reserveRowActions(row({ approvalStatus: 'Rejected' }), handler, false).showRetract).toBe(
      false,
    );
  });

  it('UI_DET_10_Retry_only_for_an_approved_row_whose_posting_failed', () => {
    expect(
      reserveRowActions(
        row({ approvalStatus: 'Approved', postingStatus: 'Failed' }),
        handler,
        false,
      ).showRetryPosting,
    ).toBe(true);
    expect(
      reserveRowActions(
        row({ approvalStatus: 'AutoApproved', postingStatus: 'Failed' }),
        handler,
        true,
      ).showRetryPosting,
    ).toBe(true);
    expect(
      reserveRowActions(
        row({ approvalStatus: 'Approved', postingStatus: 'Posted' }),
        handler,
        false,
      ).showRetryPosting,
    ).toBe(false);
  });
});

describe('UI-DET-07 Add Reserve panel rules (D-05, D-22, BR-C-06)', () => {
  const indemnity = {
    id: 'r',
    component: 'Indemnity' as const,
    currentAmount: 25_000,
    pendingAmount: 0,
    hasPendingApproval: false,
    status: 'Active' as const,
  };

  it('D_05_First_transaction_opens_the_component_later_ones_adjust_it', () => {
    expect(inferTransactionType(undefined)).toBe('Add');
    expect(inferTransactionType(indemnity)).toBe('Adjust');
  });

  it('D_05_Reverse_releases_the_whole_balance', () => {
    expect(effectiveAmount('Reverse', 999, indemnity)).toBe(-25_000);
    expect(effectiveAmount('Adjust', -5_000, indemnity)).toBe(-5_000);
  });

  it('D_05_A_cost_balance_cannot_go_below_zero; subrogation can', () => {
    expect(balanceError('Indemnity', indemnity, -25_000.01)).toBe(
      'Reserve balance for Indemnity cannot go below zero.',
    );
    expect(balanceError('Indemnity', indemnity, -25_000)).toBeNull();
    expect(balanceError('SubrogationRecoverable', undefined, -1_000)).toBeNull();
  });

  it('BR_C_06_D_22_D_26_Blockers_use_the_API_wording', () => {
    expect(submissionBlocker({ hasPolicy: false, readOnlyStatus: null, existing: undefined })).toBe(
      'No policy linked. Policy must be associated before reserves can be set.',
    );
    expect(
      submissionBlocker({
        hasPolicy: true,
        readOnlyStatus: null,
        existing: { ...indemnity, hasPendingApproval: true },
      }),
    ).toBe('Component has a pending transaction; retract it or wait for a decision.');
    expect(
      submissionBlocker({ hasPolicy: true, readOnlyStatus: 'Closed', existing: undefined }),
    ).toBe('Claim is Closed; no changes are permitted.');
    expect(
      submissionBlocker({ hasPolicy: true, readOnlyStatus: null, existing: indemnity }),
    ).toBeNull();
  });
});
