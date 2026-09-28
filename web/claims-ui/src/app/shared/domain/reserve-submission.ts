import { ReserveComponentType, ReserveTransactionType } from '../../core/models/enums';
import { ReserveComponentSummary } from '../../core/models/reserve.models';

// Mirrors Claim.Reserves.cs so the panel can explain a refusal before submitting; the API applies the rules again.

/** The first transaction on a component is an Add (D-05). */
export function inferTransactionType(
  existing: ReserveComponentSummary | undefined,
): ReserveTransactionType {
  return existing ? 'Adjust' : 'Add';
}

/** Reverse releases the whole balance. */
export function effectiveAmount(
  type: ReserveTransactionType,
  amount: number | null,
  existing: ReserveComponentSummary | undefined,
): number | null {
  if (type === 'Reverse') {
    return existing ? -existing.currentAmount : null;
  }
  return amount;
}

export function mayGoNegative(component: ReserveComponentType): boolean {
  return component === 'SubrogationRecoverable';
}

/** In the API's words: BR-C-06, D-26 and D-22 (one pending transaction per component). */
export function submissionBlocker(options: {
  hasPolicy: boolean;
  readOnlyStatus: string | null;
  existing: ReserveComponentSummary | undefined;
}): string | null {
  if (options.readOnlyStatus) {
    return `Claim is ${options.readOnlyStatus}; no changes are permitted.`;
  }
  if (!options.hasPolicy) {
    return 'No policy linked. Policy must be associated before reserves can be set.';
  }
  if (options.existing?.hasPendingApproval) {
    return 'Component has a pending transaction; retract it or wait for a decision.';
  }
  return null;
}

export function balanceError(
  component: ReserveComponentType,
  existing: ReserveComponentSummary | undefined,
  amount: number | null,
): string | null {
  if (amount == null || mayGoNegative(component)) {
    return null;
  }
  const newBalance = (existing?.currentAmount ?? 0) + amount;
  return newBalance < 0 ? `Reserve balance for ${component} cannot go below zero.` : null;
}
