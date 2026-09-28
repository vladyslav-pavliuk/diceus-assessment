import {
  AggregateContext,
  AUTHORITY_LABELS,
  exceedsAggregateLimit,
  requiredAuthority,
} from './authority';

// RSV-09: tiers mirror ReserveAuthorityPolicy: ≤ $10,000 auto, ≤ $100,000 supervisor, above that manager, by |amount| (D-05).

describe('RSV-09 authority indicator', () => {
  it.each([
    [0.01, 'Auto'],
    [9_999.99, 'Auto'],
    [10_000, 'Auto'],
    [10_000.01, 'Supervisor'],
    [50_000, 'Supervisor'],
    [100_000, 'Supervisor'],
    [100_000.01, 'Manager'],
    [2_500_000, 'Manager'],
  ] as const)('RSV_09_Authority_indicator_boundaries: %s → %s', (amount, tier) => {
    expect(requiredAuthority(amount)).toBe(tier);
  });

  it.each([
    [-10_000, 'Auto'],
    [-10_000.01, 'Supervisor'],
    [-100_000.01, 'Manager'],
  ] as const)('RSV_09_Negative_amounts_use_the_absolute_value: %s → %s', (amount, tier) => {
    expect(requiredAuthority(amount)).toBe(tier);
  });

  it.each([null, undefined, 0, Number.NaN, Number.POSITIVE_INFINITY])(
    'RSV_09_No_indicator_without_a_usable_amount: %s',
    (amount) => {
      expect(requiredAuthority(amount)).toBeNull();
    },
  );

  it('RSV_09_Labels_are_the_FRS_§11.2_texts', () => {
    expect(AUTHORITY_LABELS).toEqual({
      Auto: '✓ Auto-approved (≤ $10,000)',
      Supervisor: '⚠ Supervisor approval required',
      Manager: '⚠ Manager approval required',
    });
  });
});

// BR-R-05 in the Add Reserve preview: mirrors Claim.WouldExceedAggregateLimit.
describe('RSV-09 / BR-R-05 aggregate escalation in the preview', () => {
  const nearLimit: AggregateContext = {
    component: 'Indemnity',
    approvedAggregate: 9_998_000,
    aggregateLimit: 10_000_000,
    overrideSet: false,
  };

  it('BR_R_05_A_small_increase_that_crosses_10M_previews_Manager', () => {
    expect(requiredAuthority(5_000)).toBe('Auto');
    expect(requiredAuthority(5_000, nearLimit)).toBe('Manager');
    expect(exceedsAggregateLimit(5_000, nearLimit)).toBe(true);
  });

  it('BR_R_05_Exactly_10M_keeps_the_normal_tier', () => {
    expect(requiredAuthority(2_000, nearLimit)).toBe('Auto');
    expect(requiredAuthority(2_000.01, nearLimit)).toBe('Manager');
  });

  it('BR_R_05_Override_decreases_and_subrogation_keep_the_normal_tier', () => {
    expect(requiredAuthority(5_000, { ...nearLimit, overrideSet: true })).toBe('Auto');
    expect(requiredAuthority(-50_000, nearLimit)).toBe('Supervisor');
    expect(requiredAuthority(5_000, { ...nearLimit, component: 'SubrogationRecoverable' })).toBe(
      'Auto',
    );
  });

  it('BR_R_05_No_amount_means_no_indicator_even_near_the_limit', () => {
    expect(requiredAuthority(null, nearLimit)).toBeNull();
    expect(exceedsAggregateLimit(null, nearLimit)).toBe(false);
  });
});
