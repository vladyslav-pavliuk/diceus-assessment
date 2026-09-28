// BR-C-02 / FRS §11.2 in-force indicator, mirrored from the Domain (D-32): the loss date's UTC
// calendar date must fall inside [EffectiveDate, ExpirationDate], inclusive at both ends. The
// comparison uses the dates, not Policy.Status (D-33): a policy that has expired since the loss was
// still in force when the loss happened.

export interface PolicyPeriod {
  /** 'yyyy-MM-dd' (DateOnly). */
  effectiveDate: string;
  /** 'yyyy-MM-dd' (DateOnly). */
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

/** The UTC calendar date of an instant, as 'yyyy-MM-dd' (the server's DateOnly(LossDate.UtcDateTime)). */
export function utcDay(instant: Date): string {
  return instant.toISOString().slice(0, 10);
}
