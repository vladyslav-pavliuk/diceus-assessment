import {
  ClaimSeverity,
  ClaimStatus,
  IssueSeverity,
  PostingStatus,
  ReserveApprovalStatus,
} from '../../core/models/enums';

/** Exactly as FRS §11.1 lists them. */
export type StatusColour = 'grey' | 'blue' | 'orange' | 'purple' | 'green' | 'amber' | 'dark-grey';

export const STATUS_COLOURS: Readonly<Record<ClaimStatus, StatusColour>> = {
  Draft: 'grey',
  Open: 'blue',
  UnderInvestigation: 'orange',
  PendingPayment: 'purple',
  Closed: 'green',
  Reopened: 'amber',
  Withdrawn: 'dark-grey',
};

/** For everything whose colour the FRS does not fix. */
export type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'accent';

export const SEVERITY_TONES: Readonly<Record<ClaimSeverity, BadgeTone>> = {
  Catastrophic: 'danger',
  Critical: 'warning',
  Standard: 'info',
  Minor: 'neutral',
};

export const APPROVAL_TONES: Readonly<Record<ReserveApprovalStatus, BadgeTone>> = {
  AutoApproved: 'success',
  Approved: 'success',
  PendingApproval: 'warning',
  Rejected: 'danger',
  Cancelled: 'neutral',
};

export const POSTING_TONES: Readonly<Record<PostingStatus, BadgeTone>> = {
  Pending: 'info',
  Posted: 'success',
  Failed: 'danger',
  Cancelled: 'neutral',
};

export const ISSUE_SEVERITY_TONES: Readonly<Record<IssueSeverity, BadgeTone>> = {
  Critical: 'danger',
  Warning: 'warning',
};

export function eventTypeTone(eventType: string): BadgeTone {
  if (
    eventType === 'GL_POSTING_FAILED' ||
    eventType === 'SLA_BREACH_DETECTED' ||
    eventType === 'RESERVE_REJECTED'
  ) {
    return 'danger';
  }
  if (
    eventType === 'GL_POSTING_SIMULATED' ||
    eventType === 'RESERVE_APPROVED' ||
    eventType === 'RESERVE_AUTO_APPROVED'
  ) {
    return 'success';
  }
  if (eventType.startsWith('VALIDATION_')) {
    return 'warning';
  }
  if (eventType.startsWith('RESERVE_') || eventType.startsWith('GL_')) {
    return 'accent';
  }
  if (eventType.startsWith('CLAIM_') || eventType === 'STATUS_CHANGED') {
    return 'info';
  }
  return 'neutral';
}
