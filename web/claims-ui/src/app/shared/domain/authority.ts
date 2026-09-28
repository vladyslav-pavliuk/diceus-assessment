import { ApprovalAuthority, ReserveComponentType } from '../../core/models/enums';

// Mirrors ReserveAuthorityPolicy (BR-R-02, D-05). The UI only previews the tier; the API decides it on submit.

export const AUTO_APPROVAL_LIMIT = 10_000;
export const SUPERVISOR_LIMIT = 100_000;

/** Lets the preview show the API's escalation to Manager past the $10M limit (BR-R-05, D-11). */
export interface AggregateContext {
  component: ReserveComponentType;
  approvedAggregate: number;
  aggregateLimit: number;
  overrideSet: boolean;
}

/** FRS §8 wording. */
export const AGGREGATE_LIMIT_WARNING =
  'Total reserves will exceed $10,000,000. Manager override required.';

/** Mirrors Claim.WouldExceedAggregateLimit: exactly the limit is still allowed (`>`, not `≥`). */
export function exceedsAggregateLimit(
  amount: number | null | undefined,
  context: AggregateContext | null | undefined,
): boolean {
  if (context == null || amount == null || !Number.isFinite(amount)) {
    return false;
  }
  return (
    !context.overrideSet &&
    context.component !== 'SubrogationRecoverable' &&
    amount > 0 &&
    context.approvedAggregate + amount > context.aggregateLimit
  );
}

/** Null when there is no usable amount to judge. */
export function requiredAuthority(
  amount: number | null | undefined,
  context?: AggregateContext | null,
): ApprovalAuthority | null {
  const tier = tierFor(amount);
  return tier !== null && exceedsAggregateLimit(amount, context) ? 'Manager' : tier;
}

function tierFor(amount: number | null | undefined): ApprovalAuthority | null {
  if (amount == null || !Number.isFinite(amount) || amount === 0) {
    return null;
  }

  const magnitude = Math.abs(amount);
  if (magnitude <= AUTO_APPROVAL_LIMIT) {
    return 'Auto';
  }
  return magnitude <= SUPERVISOR_LIMIT ? 'Supervisor' : 'Manager';
}

/** Verbatim from FRS §11.2. */
export const AUTHORITY_LABELS: Readonly<Record<ApprovalAuthority, string>> = {
  Auto: '✓ Auto-approved (≤ $10,000)',
  Supervisor: '⚠ Supervisor approval required',
  Manager: '⚠ Manager approval required',
};
