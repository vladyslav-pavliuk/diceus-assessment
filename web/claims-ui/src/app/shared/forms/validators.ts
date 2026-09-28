import { AbstractControl, FormArray, ValidationErrors, ValidatorFn } from '@angular/forms';
import { PartyRole, PartyType, ReserveComponentType } from '../../core/models/enums';

// Each error carries the API's own message, so the text is the same whichever side caught the problem. The API
// validates everything again; these only save a round trip.

export const MESSAGES = {
  lossDateRequired: 'Loss date is required.',
  lossDateInFuture: 'Loss date cannot be in the future.',
  lossDescriptionTooShort: 'Loss description is required and must be at least 20 characters.',
  causeOfLossRequired: 'Cause of loss code is not recognised or is inactive.',
  claimantRequired: 'At least one Claimant party is required to open a claim.',
  personNameRequired: 'First name and last name are required for a person.',
  companyNameRequired: 'Company name is required for a company.',
  emailInvalid: 'Email address is not valid.',
  reserveAmountNotPositive: 'Reserve amount must be greater than zero.',
  subrogationAmountZero: 'Reserve amount must not be zero.',
  reserveComponentRequired: 'Invalid reserve component type.',
  estimatedLossNegative: 'Estimated loss amount cannot be negative.',
  assetDescriptionRequired: 'Asset description is required.',
  policyRequired: 'Select a policy, or switch on "Unknown policy".',
} as const;

export const LOSS_DESCRIPTION_MIN_LENGTH = 20;
export const MAX_DECIMAL_PLACES = 4;

/** BR-C-01, with no tolerance (D-32). */
export function lossDateValidator(now: () => Date): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as Date | null;
    if (value == null || !(value instanceof Date) || Number.isNaN(value.getTime())) {
      return { lossDateRequired: { message: MESSAGES.lossDateRequired } };
    }
    return value.getTime() > now().getTime()
      ? { futureDate: { message: MESSAGES.lossDateInFuture } }
      : null;
  };
}

/** BR-C-07: counted after trimming, as LossEvent does. */
export function lossDescriptionValidator(control: AbstractControl): ValidationErrors | null {
  const length = trimmedLength(control.value);
  return length >= LOSS_DESCRIPTION_MIN_LENGTH
    ? null
    : { lossDescriptionTooShort: { message: MESSAGES.lossDescriptionTooShort, actual: length } };
}

export function trimmedLength(value: unknown): number {
  return typeof value === 'string' ? value.trim().length : 0;
}

/** The UI requires a Claimant at intake, although the API would accept a Draft without one (D-06). */
export function atLeastOneClaimant(control: AbstractControl): ValidationErrors | null {
  const parties = (control as FormArray).value as { role: PartyRole | null }[];
  return parties.some((party) => party.role === 'Claimant')
    ? null
    : { claimantRequired: { message: MESSAGES.claimantRequired } };
}

/** Reads the sibling `type` control, so the group must re-validate the names when the type changes. */
export function requiredForPartyType(partyType: PartyType): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const type = control.parent?.get('type')?.value as PartyType | null | undefined;
    if (type !== partyType || trimmedLength(control.value) > 0) {
      return null;
    }
    return partyType === 'Person'
      ? { personNameRequired: { message: MESSAGES.personNameRequired } }
      : { companyNameRequired: { message: MESSAGES.companyNameRequired } };
  };
}

/**
 * BR-R-01 for an Add: SubrogationRecoverable may be negative, but never zero. Reads the sibling `component` control;
 * an empty amount is left to `required`.
 */
export function openingReserveAmountValidator(control: AbstractControl): ValidationErrors | null {
  const amount = toNumber(control.value);
  if (amount == null) {
    return null;
  }

  const component = control.parent?.get('component')?.value as
    ReserveComponentType | null | undefined;
  if (component === 'SubrogationRecoverable') {
    if (amount === 0) {
      return { reserveAmount: { message: MESSAGES.subrogationAmountZero } };
    }
  } else if (amount <= 0) {
    return { reserveAmount: { message: MESSAGES.reserveAmountNotPositive } };
  }

  return decimalPlaces(amount) > MAX_DECIMAL_PLACES
    ? { decimalPlaces: { message: 'Reserve amount must have at most 4 decimal places.' } }
    : null;
}

/** Signed and non-zero (D-05). */
export function adjustmentAmountValidator(control: AbstractControl): ValidationErrors | null {
  const amount = toNumber(control.value);
  if (amount == null) {
    return null;
  }
  if (amount === 0) {
    return { reserveAmount: { message: 'Adjustment amount must not be zero.' } };
  }
  return decimalPlaces(amount) > MAX_DECIMAL_PLACES
    ? { decimalPlaces: { message: 'Reserve amount must have at most 4 decimal places.' } }
    : null;
}

export function estimatedLossValidator(control: AbstractControl): ValidationErrors | null {
  const amount = toNumber(control.value);
  if (amount == null) {
    return null;
  }
  if (amount < 0) {
    return { estimatedLoss: { message: MESSAGES.estimatedLossNegative } };
  }
  return decimalPlaces(amount) > MAX_DECIMAL_PLACES
    ? { decimalPlaces: { message: 'Estimated loss amount must have at most 4 decimal places.' } }
    : null;
}

export function requiredWithMessage(message: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    const empty = value == null || (typeof value === 'string' && value.trim().length === 0);
    return empty ? { required: { message } } : null;
  };
}

export function toNumber(value: unknown): number | null {
  if (value == null || value === '') {
    return null;
  }
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function decimalPlaces(value: number): number {
  const text = Math.abs(value).toString();
  if (text.includes('e-')) {
    return Number(text.split('e-')[1]);
  }
  const dot = text.indexOf('.');
  return dot < 0 ? 0 : text.length - dot - 1;
}

export function errorMessage(control: AbstractControl | null | undefined): string {
  const errors = control?.errors;
  if (!errors) {
    return '';
  }

  const [key, value] = Object.entries(errors)[0];
  if (value && typeof value === 'object' && 'message' in value) {
    return String((value as { message: unknown }).message);
  }
  switch (key) {
    case 'required':
      return 'This field is required.';
    case 'email':
      return MESSAGES.emailInvalid;
    case 'maxlength':
      return `At most ${(value as { requiredLength: number }).requiredLength} characters.`;
    case 'server':
      return String(value);
    case 'matDatepickerParse':
    case 'matTimepickerParse':
      return 'Enter a valid date and time.';
    default:
      return 'This value is not valid.';
  }
}
