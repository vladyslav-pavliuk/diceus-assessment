// Mirrors ClaimDtos.cs field for field. DateTimeOffset → ISO string; decimal → number; Guid → string.

import {
  AssetType,
  ClaimSeverity,
  ClaimStatus,
  DocumentType,
  IssueSeverity,
  IssueStatus,
  PartyRole,
  PartyType,
  PerilCategory,
  ReserveComponentType,
} from './enums';
import { ReserveComponentSummary, ReserveSubmitted } from './reserve.models';

export interface ClaimSummary {
  id: string;
  claimNumber: string;
  policyId: string | null;
  policyNumber: string | null;
  clientName: string | null;
  lossDate: string;
  causeOfLossCode: string;
  causeOfLossName: string | null;
  status: ClaimStatus;
  severity: ClaimSeverity;
  assignedHandlerId: string | null;
  assignedHandlerName: string | null;
  totalReserves: number;
  isSlaBreached: boolean;
  reportedDate: string;
}

/** `status` repeats in the query string. */
export interface ClaimListQuery {
  status?: ClaimStatus[];
  dateFrom?: string | null;
  dateTo?: string | null;
  assignedHandlerId?: string | null;
  causeOfLossCode?: string | null;
  policyId?: string | null;
  search?: string | null;
  page: number;
  pageSize: number;
}

export interface ClaimDetail {
  id: string;
  claimNumber: string;
  policyId: string | null;
  policyNumber: string | null;
  clientName: string | null;
  status: ClaimStatus;
  severity: ClaimSeverity;
  reportedDate: string;
  assignedHandlerId: string | null;
  assignedHandlerName: string | null;
  closedAt: string | null;
  closureReason: string | null;
  notes: string | null;
  reserveLimitOverride: boolean;
  reserveLimitOverrideReason: string | null;
  reserveLimitOverrideAt: string | null;
  isSlaBreached: boolean;
  totalReserves: number;
  createdAt: string;
  updatedAt: string | null;
  lossEvent: LossEvent;
  parties: ClaimParty[];
  riskObjects: RiskObject[];
  validationIssues: ValidationIssue[];
  reserveComponents: ReserveComponentSummary[];
  documents: ClaimDocument[];
  recentAuditEntries: AuditEntry[];
}

export interface LossEvent {
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  causeOfLossName: string | null;
  perilCategory: PerilCategory | null;
  estimatedLossAmount: number | null;
  reportDate: string;
  policeReportNumber: string | null;
}

export interface ClaimParty {
  id: string;
  partyRole: PartyRole;
  partyType: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  displayName: string;
  email: string | null;
  phone: string | null;
  notes: string | null;
  isActive: boolean;
}

export interface RiskObject {
  id: string;
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  assetReference: string | null;
  isPrimary: boolean;
}

export interface ValidationIssue {
  id: string;
  ruleCode: string;
  severity: IssueSeverity;
  field: string;
  message: string;
  status: IssueStatus;
  raisedAt: string;
  resolvedAt: string | null;
  resolvedByUserId: string | null;
  resolutionNote: string | null;
}

export interface ClaimDocument {
  id: string;
  documentType: DocumentType;
  documentName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string | null;
  notes: string | null;
}

/** A null createdByUserId is a background job (D-33). */
export interface AuditEntry {
  id: string;
  eventType: string;
  description: string;
  oldValue: string | null;
  newValue: string | null;
  relatedEntityId: string | null;
  relatedEntityType: string | null;
  correlationId: string | null;
  createdAt: string;
  createdByUserId: string | null;
  createdByName: string | null;
}

export interface PartyInput {
  role: PartyRole;
  type: PartyType;
  firstName: string | null;
  lastName: string | null;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
}

export interface RiskObjectInput {
  assetType: AssetType;
  assetDescription: string;
  damageDescription: string | null;
  assetReference: string | null;
  isPrimary: boolean;
}

export interface InitialReserveInput {
  component: ReserveComponentType;
  amount: number;
  changeReason: string | null;
}

/** A null policyId is the "Unknown policy" intake. */
export interface CreateClaimRequest {
  policyId: string | null;
  lossDate: string;
  lossDescription: string;
  lossLocation: string | null;
  causeOfLossCode: string;
  estimatedLossAmount: number | null;
  policeReportNumber: string | null;
  severity: ClaimSeverity | null;
  parties: PartyInput[];
  riskObjects: RiskObjectInput[];
  initialReserve: InitialReserveInput | null;
}

export interface ClaimCreated {
  id: string;
  claimNumber: string;
  status: ClaimStatus;
  validationIssues: ValidationIssue[];
  initialReserve: ReserveSubmitted | null;
}

export interface TransitionClaimStatusRequest {
  targetStatus: ClaimStatus;
  reason: string | null;
  justification: string | null;
}

export interface ClaimStatusChanged {
  claimId: string;
  previousStatus: ClaimStatus;
  status: ClaimStatus;
}

/** Null leaves a field unchanged; "" clears the notes (D-40). */
export interface UpdateClaimDetailsRequest {
  notes: string | null;
  severity: ClaimSeverity | null;
}
