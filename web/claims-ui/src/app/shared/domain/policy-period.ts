// Mirrors Policy.CoversLossDate (BR-C-02, D-32). Compares dates, not Policy.Status: a policy that has expired
// since the loss was still in force when it happened (D-33).

export interface PolicyPeriod {
  /** 'yyyy-MM-dd'. */
  effectiveDate: string;
  /** 'yyyy-MM-dd'. */
  expirationDate: string;
}

export type PolicyPeriodState = 'in-force' | 'outside-period' | 'no-loss-date';

export function policyPeriodState(policy: PolicyPeriod, lossDate: Date | null): PolicyPeriodState {
  if (lossDate == null || Number.isNaN(lossDate.getTime())) {
    return 'no-loss-date';
  }

  // ISO 'yyyy-MM-dd' strings compare correctly as text.
  const lossDay = utcDay(lossDate);
  return lossDay >= policy.effectiveDate && lossDay <= policy.expirationDate
    ? 'in-force'
    : 'outside-period';
}

/** As the server's DateOnly.FromDateTime(lossDate.UtcDateTime). */
export function utcDay(instant: Date): string {
  return instant.toISOString().slice(0, 10);
}
