import { ApprovalAuthority } from '../../core/models/enums';

// FRS §6.3 / BR-R-02, mirrored from ReserveAuthorityPolicy in the Domain. The tier depends on the
// amount of the single transaction, by absolute value, so a large release needs the same authority as a
// large increase (D-05). The UI only previews the tier; the API decides it again on submit.

export const AUTO_APPROVAL_LIMIT = 10_000;
export const SUPERVISOR_LIMIT = 100_000;

/** The approval tier for a transaction amount; null when there is no usable amount to judge. */
export function requiredAuthority(amount: number | null | undefined): ApprovalAuthority | null {
  if (amount == null || !Number.isFinite(amount) || amount === 0) {
    return null;
  }

  const magnitude = Math.abs(amount);
  if (magnitude <= AUTO_APPROVAL_LIMIT) {
    return 'Auto';
  }
  return magnitude <= SUPERVISOR_LIMIT ? 'Supervisor' : 'Manager';
}

/** The indicator texts, verbatim from FRS §11.2 step 3. */
export const AUTHORITY_LABELS: Readonly<Record<ApprovalAuthority, string>> = {
  Auto: '✓ Auto-approved (≤ $10,000)',
  Supervisor: '⚠ Supervisor approval required',
  Manager: '⚠ Manager approval required',
};
