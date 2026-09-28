// Mirrors of the reference data, policy, user, paging and auth DTOs.

import { ClaimStatus, PerilCategory, PolicyStatus, UserRole } from './enums';

/** PagedResult<T>: one page plus the metadata a paginated table needs (D-29). */
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CauseOfLossCode {
  code: string;
  name: string;
  perilCategory: PerilCategory;
  sortOrder: number;
}

export interface ClaimStatusTransition {
  fromStatus: ClaimStatus;
  toStatus: ClaimStatus;
  minimumRole: UserRole | null;
  requiresReason: boolean;
  isSystemOnly: boolean;
}

/** GET /api/reference/claim-statuses (ClaimStatusDto): each status with its outgoing transitions (D-09). */
export interface ClaimStatusDefinition {
  status: ClaimStatus;
  transitions: ClaimStatusTransition[];
}

/** PolicyDto. effectiveDate/expirationDate are DateOnly: 'yyyy-MM-dd'. */
export interface Policy {
  id: string;
  policyNumber: string;
  clientName: string;
  effectiveDate: string;
  expirationDate: string;
  status: PolicyStatus;
  coverageTypes: string[];
}

export interface PolicyCoverage {
  policyId: string;
  policyNumber: string;
  coverageTypes: string[];
}

export interface User {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
  organisationId: string;
}

/** POST /api/auth/dev-token (DevTokenResponse). */
export interface DevTokenResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: User;
}
