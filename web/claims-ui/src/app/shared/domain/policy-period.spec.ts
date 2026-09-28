import { intakeWarnings } from './intake-warnings';
import { policyPeriodState, utcDay } from './policy-period';

const policy = { effectiveDate: '2025-01-01', expirationDate: '2026-05-31' };

describe('UI-FNOL-03 / BR-C-02 in-force indicator (D-32)', () => {
  it('UI_FNOL_03_In_force_badge is green inside the period, both ends inclusive', () => {
    expect(policyPeriodState(policy, new Date('2025-01-01T00:00:00Z'))).toBe('in-force');
    expect(policyPeriodState(policy, new Date('2026-05-31T23:59:59Z'))).toBe('in-force');
  });

  it('UI_FNOL_03_In_force_badge is amber outside the period', () => {
    expect(policyPeriodState(policy, new Date('2024-12-31T23:59:59Z'))).toBe('outside-period');
    expect(policyPeriodState(policy, new Date('2026-06-01T00:00:00Z'))).toBe('outside-period');
  });

  it('UI_FNOL_03_Compares_the_UTC_calendar_date_like_the_API', () => {
    // 23:30 on the expiry date in UTC-5 is already the next day in UTC: outside, as on the server (D-32).
    const lateEvening = new Date('2026-05-31T23:30:00-05:00');
    expect(utcDay(lateEvening)).toBe('2026-06-01');
    expect(policyPeriodState(policy, lateEvening)).toBe('outside-period');
  });

  it('UI_FNOL_03_Neutral_until_a_loss_date_is_entered', () => {
    expect(policyPeriodState(policy, null)).toBe('no-loss-date');
    expect(policyPeriodState(policy, new Date('nope'))).toBe('no-loss-date');
  });
});

describe('UI-FNOL-12 warning confirmation (D-06)', () => {
  const inPeriod = new Date('2026-01-15T10:00:00Z');

  it('UI_FNOL_12_No_warnings_for_a_complete_in_period_claim', () => {
    expect(intakeWarnings({ policy, lossDate: inPeriod, riskObjectCount: 1 })).toEqual([]);
  });

  it('UI_FNOL_12_Warning_confirmation_dialog lists each intake warning with the API wording', () => {
    expect(
      intakeWarnings({ policy, lossDate: new Date('2026-07-01T00:00:00Z'), riskObjectCount: 0 }),
    ).toEqual(['Loss date is outside the policy effective period.', 'No risk objects linked.']);
  });

  it('UI_FNOL_12_Unknown_policy_gives_the_BR_C_06_warning_and_no_period_warning', () => {
    expect(intakeWarnings({ policy: null, lossDate: inPeriod, riskObjectCount: 2 })).toEqual([
      'No policy linked. Policy must be associated before reserves can be set.',
    ]);
  });
});
