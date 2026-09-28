import { ApprovalAuthority, UserRole } from '../models/enums';

// The UI's copy of the role hierarchy (FRS §3: handler < supervisor < manager). It only decides what
// to show; the API enforces every rule again (FRS §3 "backend must validate caller's role").

const RANK: Record<UserRole, number> = { Handler: 1, Supervisor: 2, Manager: 3 };

/** Mirrors UserRole.IsAtLeast in the Domain. */
export function isAtLeast(role: UserRole | null | undefined, minimum: UserRole): boolean {
  return role != null && RANK[role] >= RANK[minimum];
}

/** Mirrors ReserveAuthorityPolicy.CanApprove: may this role decide a transaction of this tier? */
export function canApproveTier(
  role: UserRole | null | undefined,
  required: ApprovalAuthority,
): boolean {
  switch (required) {
    case 'Auto':
      return role != null;
    case 'Supervisor':
      return isAtLeast(role, 'Supervisor');
    case 'Manager':
      return isAtLeast(role, 'Manager');
  }
}
