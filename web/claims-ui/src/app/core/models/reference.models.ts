import { ClaimStatus, PerilCategory, PolicyStatus, UserRole } from './enums';

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

export interface ClaimStatusDefinition {
  status: ClaimStatus;
  transitions: ClaimStatusTransition[];
}

/** The dates are DateOnly: 'yyyy-MM-dd'. */
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

export interface DevTokenResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: User;
}
