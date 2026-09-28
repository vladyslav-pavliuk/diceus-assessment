import { ApprovalAuthority, ReserveComponentType } from '../../core/models/enums';

// FRS §6.3 / BR-R-02, mirrored from ReserveAuthorityPolicy in the Domain. The tier depends on the
// amount of the single transaction, by absolute value, so a large release needs the same authority as a
// large increase (D-05). The UI only previews the tier; the API decides it again on submit.

export const AUTO_APPROVAL_LIMIT = 10_000;
export const SUPERVISOR_LIMIT = 100_000;

/**
 * BR-R-05 context for a transaction on an existing claim, from GET /reserves. With it, the preview also shows the
 * escalation to Manager that the API applies when approving the transaction would take the approved aggregate over
 * the limit without the override (Claim.WouldExceedAggregateLimit, D-11).
 */
export interface AggregateContext {
  component: ReserveComponentType;
  approvedAggregate: number;
  aggregateLimit: number;
  overrideSet: boolean;
}

/** The FRS §8 aggregate warning (VAL-10), shown with an escalated preview. */
export const AGGREGATE_LIMIT_WARNING =
  'Total reserves will exceed $10,000,000. Manager override required.';

/**
 * Mirrors Claim.WouldExceedAggregateLimit: only increases of cost components count (SubrogationRecoverable is
 * excluded, D-11), the override lifts the limit, and exactly the limit is still allowed (`>`, not `≥`).
 */
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

/**
 * The approval tier for a transaction amount; null when there is no usable amount to judge. With an aggregate
 * context, a transaction that would cross the $10M limit needs a Manager whatever its size (BR-R-05).
 */
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

/** The indicator texts, verbatim from FRS §11.2 step 3. */
export const AUTHORITY_LABELS: Readonly<Record<ApprovalAuthority, string>> = {
  Auto: '✓ Auto-approved (≤ $10,000)',
  Supervisor: '⚠ Supervisor approval required',
  Manager: '⚠ Manager approval required',
};
