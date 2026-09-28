import { FormArray, FormControl, FormGroup } from '@angular/forms';
import {
  MESSAGES,
  atLeastOneClaimant,
  estimatedLossValidator,
  errorMessage,
  lossDateValidator,
  lossDescriptionValidator,
  openingReserveAmountValidator,
  requiredForPartyType,
} from './validators';

// The FNOL validators (FRS §8, §11.2). Test names start with the requirement IDs of
// docs/REQUIREMENTS-MATRIX.md; each error carries the API's own message.

const NOW = new Date('2026-09-28T12:00:00Z');
const clock = () => NOW;

describe('UI-FNOL-05 / BR-C-01 loss date', () => {
  const validate = (value: Date | null) => lossDateValidator(clock)(new FormControl(value));

  it('UI_FNOL_05_Future_date_validator rejects an instant one millisecond after now', () => {
    const errors = validate(new Date(NOW.getTime() + 1));
    expect(errors).toEqual({ futureDate: { message: 'Loss date cannot be in the future.' } });
  });

  it('UI_FNOL_05_Future_date_validator accepts exactly now (no tolerance, D-32)', () => {
    expect(validate(new Date(NOW.getTime()))).toBeNull();
  });

  it('UI_FNOL_05_Future_date_validator accepts a date in the past', () => {
    expect(validate(new Date('2026-09-01T14:30:00Z'))).toBeNull();
  });

  it('UI_FNOL_05_Loss_date_is_required with the FRS §8 message', () => {
    expect(validate(null)).toEqual({ lossDateRequired: { message: 'Loss date is required.' } });
  });

  it('UI_FNOL_05_An_unparseable_date_counts_as_missing', () => {
    expect(validate(new Date('not a date'))).toEqual({
      lossDateRequired: { message: 'Loss date is required.' },
    });
  });

  it('UI_FNOL_05_Reads_the_injected_clock_at_validation_time', () => {
    let now = new Date('2026-01-01T00:00:00Z');
    const control = new FormControl<Date | null>(
      new Date('2026-06-01T00:00:00Z'),
      lossDateValidator(() => now),
    );
    expect(control.hasError('futureDate')).toBe(true);

    now = new Date('2026-07-01T00:00:00Z');
    control.updateValueAndValidity();
    expect(control.valid).toBe(true);
  });
});

describe('UI-FNOL-07 / BR-C-07 loss description', () => {
  const validate = (value: string) => lossDescriptionValidator(new FormControl(value));

  it('UI_FNOL_07_Min_length_counter rejects 19 characters', () => {
    expect(validate('a'.repeat(19))).toEqual({
      lossDescriptionTooShort: { message: MESSAGES.lossDescriptionTooShort, actual: 19 },
    });
  });

  it('UI_FNOL_07_Min_length_counter accepts exactly 20 characters', () => {
    expect(validate('a'.repeat(20))).toBeNull();
  });

  it('UI_FNOL_07_Whitespace_does_not_count, as the API trims before it counts', () => {
    expect(validate(`   ${'a'.repeat(19)}      `)).not.toBeNull();
    expect(validate(' '.repeat(25))).not.toBeNull();
  });

  it('UI_FNOL_07_Empty_description_uses_the_FRS_message', () => {
    expect(errorMessage(new FormControl('', lossDescriptionValidator))).toBe(
      'Loss description is required and must be at least 20 characters.',
    );
  });
});

describe('UI-FNOL-09 / BR-C-03 at least one Claimant', () => {
  const parties = (...roles: (string | null)[]) =>
    new FormArray(
      roles.map((role) => new FormGroup({ role: new FormControl(role) })),
      atLeastOneClaimant,
    );

  it('UI_FNOL_09_Claimant_required_validator rejects an empty list', () => {
    const array = parties();
    expect(array.errors).toEqual({ claimantRequired: { message: MESSAGES.claimantRequired } });
  });

  it('UI_FNOL_09_Claimant_required_validator rejects parties without a Claimant', () => {
    expect(parties('Witness', 'Insured', 'ThirdParty').hasError('claimantRequired')).toBe(true);
  });

  it('UI_FNOL_09_Claimant_required_validator accepts one Claimant among others', () => {
    expect(parties('Witness', 'Claimant').valid).toBe(true);
  });

  it('UI_FNOL_09_Removing_the_only_Claimant_makes_the_step_invalid_again', () => {
    const array = parties('Claimant', 'Witness');
    expect(array.valid).toBe(true);
    array.removeAt(0);
    expect(array.hasError('claimantRequired')).toBe(true);
  });

  it('UI_FNOL_09_Message_is_the_FRS_§8_wording', () => {
    expect(MESSAGES.claimantRequired).toBe(
      'At least one Claimant party is required to open a claim.',
    );
  });
});

describe('FRS §9.3 party names per party type', () => {
  const party = (type: 'Person' | 'Company', first = '', last = '', company = '') =>
    new FormGroup({
      type: new FormControl(type),
      firstName: new FormControl(first, requiredForPartyType('Person')),
      lastName: new FormControl(last, requiredForPartyType('Person')),
      companyName: new FormControl(company, requiredForPartyType('Company')),
    });

  it('PTY_Person_needs_first_and_last_name', () => {
    const group = party('Person', 'Maria');
    group.controls.firstName.updateValueAndValidity();
    group.controls.lastName.updateValueAndValidity();
    expect(group.controls.firstName.valid).toBe(true);
    expect(errorMessage(group.controls.lastName)).toBe(
      'First name and last name are required for a person.',
    );
  });

  it('PTY_Company_needs_a_company_name_and_no_person_names', () => {
    const group = party('Company');
    for (const control of Object.values(group.controls)) {
      control.updateValueAndValidity();
    }
    expect(group.controls.firstName.valid).toBe(true);
    expect(errorMessage(group.controls.companyName)).toBe(
      'Company name is required for a company.',
    );
  });
});

describe('BR-R-01 initial reserve amount (D-05)', () => {
  const validate = (component: string | null, amount: number | string | null) => {
    const group = new FormGroup({
      component: new FormControl(component),
      amount: new FormControl(amount, openingReserveAmountValidator),
    });
    group.controls.amount.updateValueAndValidity();
    return group.controls.amount.errors;
  };

  it('BR_R_01_Cost_component_amount_must_be_greater_than_zero', () => {
    expect(validate('Indemnity', 0)).toEqual({
      reserveAmount: { message: 'Reserve amount must be greater than zero.' },
    });
    expect(validate('Expense', -1)).toEqual({
      reserveAmount: { message: 'Reserve amount must be greater than zero.' },
    });
    expect(validate('ALAE', 0.01)).toBeNull();
  });

  it('BR_R_01_SubrogationRecoverable_may_be_negative_but_not_zero', () => {
    expect(validate('SubrogationRecoverable', -2500)).toBeNull();
    expect(validate('SubrogationRecoverable', 0)).toEqual({
      reserveAmount: { message: 'Reserve amount must not be zero.' },
    });
  });

  it('BR_R_01_At_most_four_decimal_places (DECIMAL(19,4))', () => {
    expect(validate('Indemnity', 10.1234)).toBeNull();
    expect(validate('Indemnity', 10.12345)).toEqual({
      decimalPlaces: { message: 'Reserve amount must have at most 4 decimal places.' },
    });
  });

  it('BR_R_01_An_empty_amount_is_left_to_the_required_rule', () => {
    expect(validate('Indemnity', null)).toBeNull();
    expect(validate('Indemnity', '')).toBeNull();
  });
});

describe('UI-FNOL-08 estimated loss amount', () => {
  it('UI_FNOL_08_Estimated_loss_is_optional_and_not_negative', () => {
    expect(estimatedLossValidator(new FormControl(null))).toBeNull();
    expect(estimatedLossValidator(new FormControl(45000))).toBeNull();
    expect(estimatedLossValidator(new FormControl(-1))).toEqual({
      estimatedLoss: { message: 'Estimated loss amount cannot be negative.' },
    });
  });
});
