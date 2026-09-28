import { ReserveComponentType, ReserveTransactionType } from '../../core/models/enums';
import { ReserveComponentSummary } from '../../core/models/reserve.models';

// The Add Reserve panel's rules (FRS §6, §11.3; D-05, D-22), mirrored from Claim.Reserves.cs so the panel
// can explain a refusal before the user submits. The API applies the same rules again.

/** D-05: the first transaction on a component opens it (Add); later ones adjust it or reverse it. */
export function inferTransactionType(
  existing: ReserveComponentSummary | undefined,
): ReserveTransactionType {
  return existing ? 'Adjust' : 'Add';
}

/** The signed amount a transaction moves the balance by; Reverse releases the whole balance. */
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

/** Only SubrogationRecoverable may go negative (FRS §6.2). */
export function mayGoNegative(component: ReserveComponentType): boolean {
  return component === 'SubrogationRecoverable';
}

/**
 * Why a component cannot take a new transaction right now, in the API's words, or null. Covers
 * BR-C-06 (no policy), D-26 (read-only claim) and D-22 (one pending transaction per component).
 */
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

/** D-05: a cost component's balance may not go below zero. */
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
