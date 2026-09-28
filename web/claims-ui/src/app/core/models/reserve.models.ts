// Mirrors of ClaimsModule.Application/Claims/ReserveDtos.cs and the reserve request bodies.

import {
  ApprovalAuthority,
  PostingStatus,
  ReserveApprovalStatus,
  ReserveComponentStatus,
  ReserveComponentType,
  ReserveTransactionType,
} from './enums';

/** A reserve summary card (ReserveComponentSummaryDto). */
export interface ReserveComponentSummary {
  id: string;
  component: ReserveComponentType;
  currentAmount: number;
  pendingAmount: number;
  hasPendingApproval: boolean;
  status: ReserveComponentStatus;
}

/** One ReserveHistory row as returned by the reserve commands (ReserveTransactionDto). */
export interface ReserveTransaction {
  id: string;
  reserveComponentId: string;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  requiredAuthority: ApprovalAuthority;
  exceedsAggregateLimit: boolean;
  changeReason: string;
  changeSequence: number;
  idempotencyKey: string;
  postingStatus: PostingStatus;
  submittedByUserId: string;
  approvedByUserId: string | null;
  approvedAt: string | null;
  rejectedByUserId: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
}

/** ReserveSubmittedDto: the new transaction plus non-blocking warnings (BR-R-05). */
export interface ReserveSubmitted {
  component: ReserveComponentType;
  transaction: ReserveTransaction;
  warnings: string[];
}

/** A row of the Reserves tab history table (ReserveHistoryEntryDto). */
export interface ReserveHistoryEntry {
  id: string;
  reserveComponentId: string;
  component: ReserveComponentType;
  transactionType: ReserveTransactionType;
  amount: number;
  previousBalance: number;
  newBalance: number;
  approvalStatus: ReserveApprovalStatus;
  requiredAuthority: ApprovalAuthority;
  exceedsAggregateLimit: boolean;
  changeReason: string;
  changeSequence: number;
  idempotencyKey: string;
  postingStatus: PostingStatus;
  postingJobId: string | null;
  createdAt: string;
  submittedByUserId: string;
  submittedByName: string | null;
  approvedByUserId: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
  rejectedByUserId: string | null;
  rejectedByName: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
}

/** GET /api/claims/{id}/reserves (ClaimReservesDto). */
export interface ClaimReserves {
  claimId: string;
  components: ReserveComponentSummary[];
  transactions: ReserveHistoryEntry[];
  totalReserves: number;
  approvedAggregate: number;
  aggregateLimit: number;
  reserveLimitOverride: boolean;
}

/** POST /api/claims/{id}/reserves (SubmitReserveRequest). transactionType may be omitted (D-05). */
export interface SubmitReserveRequest {
  component: ReserveComponentType;
  amount: number | null;
  changeReason: string;
  transactionType: ReserveTransactionType | null;
}
