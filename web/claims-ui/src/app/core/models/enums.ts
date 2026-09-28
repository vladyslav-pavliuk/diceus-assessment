// Serialised by name. Each list is in the backend's declaration order, which is also the display order.

export const CLAIM_STATUSES = [
  'Draft',
  'Open',
  'UnderInvestigation',
  'PendingPayment',
  'Closed',
  'Reopened',
  'Withdrawn',
] as const;
export type ClaimStatus = (typeof CLAIM_STATUSES)[number];

export const CLAIM_SEVERITIES = ['Catastrophic', 'Critical', 'Standard', 'Minor'] as const;
export type ClaimSeverity = (typeof CLAIM_SEVERITIES)[number];

export const PARTY_ROLES = ['Claimant', 'Insured', 'ThirdParty', 'Witness', 'Attorney'] as const;
export type PartyRole = (typeof PARTY_ROLES)[number];

export const PARTY_TYPES = ['Person', 'Company'] as const;
export type PartyType = (typeof PARTY_TYPES)[number];

export const ASSET_TYPES = ['Vehicle', 'Property', 'Person', 'Equipment', 'Other'] as const;
export type AssetType = (typeof ASSET_TYPES)[number];

export type IssueSeverity = 'Critical' | 'Warning';
export type IssueStatus = 'Open' | 'Resolved' | 'Acknowledged';

export const RESERVE_COMPONENTS = [
  'Indemnity',
  'Expense',
  'ALAE',
  'SubrogationRecoverable',
] as const;
export type ReserveComponentType = (typeof RESERVE_COMPONENTS)[number];

export type ReserveComponentStatus = 'Active' | 'Closed';
export type ReserveTransactionType = 'Add' | 'Adjust' | 'Reverse';
export type ReserveApprovalStatus =
  'AutoApproved' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled';
export type ApprovalAuthority = 'Auto' | 'Supervisor' | 'Manager';
export type PostingStatus = 'Pending' | 'Posted' | 'Failed' | 'Cancelled';

export const DOCUMENT_TYPES = ['PoliceReport', 'MedicalReport', 'Invoice', 'Other'] as const;
export type DocumentType = (typeof DOCUMENT_TYPES)[number];

export const USER_ROLES = ['Handler', 'Supervisor', 'Manager'] as const;
export type UserRole = (typeof USER_ROLES)[number];

export type PerilCategory =
  'Property' | 'Auto' | 'Liability' | 'Weather' | 'Equipment' | 'Crime' | 'General';

export type PolicyStatus = 'Active' | 'Expired' | 'Cancelled';

/** Display labels for enum names that are not readable as they are. */
export const ENUM_LABELS: Readonly<Record<string, string>> = {
  UnderInvestigation: 'Under investigation',
  PendingPayment: 'Pending payment',
  ThirdParty: 'Third party',
  SubrogationRecoverable: 'Subrogation recoverable',
  ALAE: 'ALAE',
  PoliceReport: 'Police report',
  MedicalReport: 'Medical report',
  AutoApproved: 'Auto-approved',
  PendingApproval: 'Pending approval',
};

export function enumLabel(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  return ENUM_LABELS[value] ?? value;
}
