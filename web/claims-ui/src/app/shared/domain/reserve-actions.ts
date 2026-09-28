import { canApproveTier, isAtLeast } from '../../core/auth/roles';
import { UserRole } from '../../core/models/enums';
import { ReserveHistoryEntry } from '../../core/models/reserve.models';

// Which buttons a row of the Reserves history table shows (FRS §11.3 Tab 3), mirrored from
// Claim.Reserves.cs. The UI hides what a role can never do (SEC-03) and disables, with the API's own
// message, what this particular row does not allow; the API re-checks everything.

export interface CurrentUser {
  id: string;
  role: UserRole;
}

export interface ReserveRowActions {
  /** Approve/Reject are shown only to supervisors and managers, on pending rows (FRS §11.3, brief §3.7.4). */
  showDecision: boolean;
  /** Why Approve is disabled, in the API's words; null when it is enabled. */
  approveBlockedReason: string | null;
  /** Why Reject is disabled; null when it is enabled. */
  rejectBlockedReason: string | null;
  /** Retract: the submitter, on their own pending row (FRS §6.4). */
  showRetract: boolean;
  /** Retry: an approved row whose GL posting failed (FRS §11.3, D-08). */
  showRetryPosting: boolean;
}

export const SELF_APPROVAL_MESSAGE = 'Self-approval is not permitted.';
export const NO_APPROVAL_AUTHORITY_MESSAGE =
  'Your role does not have authority to approve this reserve amount.';
export const NO_REJECTION_AUTHORITY_MESSAGE =
  'Your role does not have authority to reject this reserve amount.';
/** D-45: the submitter withdraws their own pending transaction with Retract, not Reject. */
export const SELF_REJECTION_MESSAGE =
  'Self-rejection is not permitted. Use Retract to withdraw your own pending reserve.';

export function reserveRowActions(
  row: ReserveHistoryEntry,
  user: CurrentUser | null,
  claimIsReadOnly: boolean,
): ReserveRowActions {
  const pending = row.approvalStatus === 'PendingApproval' && !claimIsReadOnly;
  const showDecision = pending && isAtLeast(user?.role, 'Supervisor');

  let approveBlockedReason: string | null = null;
  let rejectBlockedReason: string | null = null;
  if (showDecision && user) {
    const ownRow = row.submittedByUserId === user.id;
    if (ownRow) {
      approveBlockedReason = SELF_APPROVAL_MESSAGE;
    } else if (!canApproveTier(user.role, row.requiredAuthority)) {
      approveBlockedReason = NO_APPROVAL_AUTHORITY_MESSAGE;
    }
    if (ownRow) {
      rejectBlockedReason = SELF_REJECTION_MESSAGE;
    } else if (!canApproveTier(user.role, row.requiredAuthority)) {
      rejectBlockedReason = NO_REJECTION_AUTHORITY_MESSAGE;
    }
  }

  const approved = row.approvalStatus === 'Approved' || row.approvalStatus === 'AutoApproved';

  return {
    showDecision,
    approveBlockedReason,
    rejectBlockedReason,
    showRetract: pending && user != null && row.submittedByUserId === user.id,
    // Allowed on Closed/Withdrawn claims too: it completes the accounting of an earlier approval (D-41 item 9).
    showRetryPosting: approved && row.postingStatus === 'Failed',
  };
}

/** Closed and Withdrawn claims accept no changes except a reopen (D-26); mirrors Claim.IsReadOnlyStatus. */
export function isReadOnlyStatus(status: string): boolean {
  return status === 'Closed' || status === 'Withdrawn';
}
