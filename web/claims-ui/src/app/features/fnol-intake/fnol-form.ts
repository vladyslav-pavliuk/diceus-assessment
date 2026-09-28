import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { CreateClaimRequest } from '../../core/models/claim.models';
import { ReserveComponentType } from '../../core/models/enums';
import { CauseOfLossCode, Policy } from '../../core/models/reference.models';
import { PartyGroup, toPartyInput } from '../../shared/forms/party-form';
import {
  PolicyControl,
  isPolicy,
  policySelectedValidator,
} from '../../shared/forms/policy-typeahead';
import { RiskObjectGroup, toRiskObjectInput } from '../../shared/forms/risk-object-form';
import {
  MESSAGES,
  atLeastOneClaimant,
  estimatedLossValidator,
  lossDateValidator,
  lossDescriptionValidator,
  openingReserveAmountValidator,
  toNumber,
} from '../../shared/forms/validators';

// The FNOL form (FRS §5.2, §11.2): one FormGroup per MatStepper step, so each step validates on its own
// and the stepper cannot advance past an invalid one (brief §3.7.2).

export type CauseControl = FormControl<CauseOfLossCode | string | null>;

export function createFnolForm(now: () => Date) {
  const policyLoss = new FormGroup({
    /** FRS §11.2 "Unknown Policy toggle": proceed without a policy link (BR-C-06 warning). */
    unknownPolicy: new FormControl(false, { nonNullable: true }),
    policy: new FormControl<Policy | string | null>(null, policySelectedValidator) as PolicyControl,
    /** The loss instant: the picked day with the picked time of day (FRS §11.2 "date-time picker"). */
    lossDate: new FormControl<Date | null>(null, lossDateValidator(now)),
    /** The time-of-day picker; merged into lossDate, never sent on its own. */
    lossTime: new FormControl<Date | null>(null),
    causeOfLoss: new FormControl<CauseOfLossCode | string | null>(
      null,
      causeSelectedValidator,
    ) as CauseControl,
    lossDescription: new FormControl('', {
      nonNullable: true,
      validators: lossDescriptionValidator,
    }),
    lossLocation: new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) }),
    estimatedLossAmount: new FormControl<number | null>(null, estimatedLossValidator),
  });

  const partiesRisk = new FormGroup({
    parties: new FormArray<PartyGroup>([], atLeastOneClaimant),
    // No validator: a risk object is recommended, not required (FRS §11.2 advisory, §5.4 Warning).
    riskObjects: new FormArray<RiskObjectGroup>([]),
  });

  const reserve = new FormGroup({
    component: new FormControl<ReserveComponentType | null>(
      null,
      requiredWhenSiblingSet('amount', 'Choose a reserve component.'),
    ),
    amount: new FormControl<number | null>(null, [
      requiredWhenSiblingSet('component', 'Enter the reserve amount.'),
      openingReserveAmountValidator,
    ]),
  });

  // The two reserve fields validate each other: re-check the other one whenever one changes.
  reserve.controls.component.valueChanges.subscribe(() =>
    reserve.controls.amount.updateValueAndValidity({ emitEvent: false }),
  );
  reserve.controls.amount.valueChanges.subscribe(() =>
    reserve.controls.component.updateValueAndValidity({ emitEvent: false }),
  );

  // Two pickers, one instant. Angular writes a picker's change to the model only, not to the other
  // picker, so the date and time inputs cannot share one control: the time picker would combine its time
  // with today. lossTime therefore feeds lossDate, and the merged value is written without touching the
  // date input's text (emitModelToViewChange: false), so typing a date is never reformatted mid-keystroke.
  const { lossDate, lossTime } = policyLoss.controls;
  const mergeTime = () => {
    const merged = withTimeOfDay(lossDate.value, lossTime.value);
    if (merged && merged.getTime() !== lossDate.value?.getTime()) {
      lossDate.setValue(merged, { emitModelToViewChange: false });
    }
  };
  lossDate.valueChanges.subscribe(mergeTime);
  lossTime.valueChanges.subscribe(() => {
    mergeTime();
    lossDate.markAsTouched();
  });

  // "Unknown policy" switches the policy field off, and with it the initial reserve: the API refuses
  // a reserve on a claim with no policy (BR-C-06, D-06), so the form does not offer one.
  policyLoss.controls.unknownPolicy.valueChanges.subscribe((unknown) => {
    if (unknown) {
      policyLoss.controls.policy.reset(null);
      policyLoss.controls.policy.disable();
      reserve.reset({ component: null, amount: null });
      reserve.disable();
    } else {
      policyLoss.controls.policy.enable();
      reserve.enable();
    }
  });

  return new FormGroup({ policyLoss, partiesRisk, reserve });
}

export type FnolForm = ReturnType<typeof createFnolForm>;

/** Where each API error key belongs in the form (D-40 item 4 keys → control paths). */
export const FNOL_ERROR_ROOTS: Readonly<Record<string, string>> = {
  PolicyId: 'policyLoss.policy',
  LossDate: 'policyLoss.lossDate',
  LossDescription: 'policyLoss.lossDescription',
  LossLocation: 'policyLoss.lossLocation',
  CauseOfLossCode: 'policyLoss.causeOfLoss',
  EstimatedLossAmount: 'policyLoss.estimatedLossAmount',
  Parties: 'partiesRisk.parties',
  ClaimParties: 'partiesRisk.parties',
  RiskObjects: 'partiesRisk.riskObjects',
  InitialReserve: 'reserve',
};

/** The step (0, 1, 2) an error key belongs to; unknown keys go to the last step, next to Create. */
export function stepOfErrorKey(key: string): number {
  const path = FNOL_ERROR_ROOTS[key.split(/[.[]/)[0]] ?? '';
  if (path.startsWith('policyLoss')) {
    return 0;
  }
  if (path.startsWith('partiesRisk')) {
    return 1;
  }
  return 2;
}

/**
 * POST /api/claims (CreateClaimCommand) from the form. Also used for the review table while the form is
 * still incomplete, so every field tolerates being empty.
 */
export function toCreateClaimRequest(form: FnolForm): CreateClaimRequest {
  const step1 = form.controls.policyLoss.getRawValue();
  const reserve = form.controls.reserve;
  const cause = step1.causeOfLoss;

  return {
    policyId: !step1.unknownPolicy && isPolicy(step1.policy) ? step1.policy.id : null,
    lossDate:
      step1.lossDate && !Number.isNaN(step1.lossDate.getTime()) ? step1.lossDate.toISOString() : '',
    lossDescription: step1.lossDescription.trim(),
    lossLocation: step1.lossLocation.trim() || null,
    causeOfLossCode: isCause(cause) ? cause.code : '',
    estimatedLossAmount: toNumber(step1.estimatedLossAmount),
    policeReportNumber: null,
    severity: null,
    parties: form.controls.partiesRisk.controls.parties.controls.map(toPartyInput),
    riskObjects: form.controls.partiesRisk.controls.riskObjects.controls.map(toRiskObjectInput),
    initialReserve:
      reserve.enabled && reserve.value.component && toNumber(reserve.value.amount) != null
        ? {
            component: reserve.value.component,
            amount: toNumber(reserve.value.amount)!,
            changeReason: null,
          }
        : null,
  };
}

/** The day of `day` at the hours and minutes of `time`; the day unchanged when there is no time. */
export function withTimeOfDay(day: Date | null, time: Date | null): Date | null {
  if (!day || Number.isNaN(day.getTime())) {
    return null;
  }
  if (!time || Number.isNaN(time.getTime())) {
    return day;
  }
  const merged = new Date(day);
  merged.setHours(time.getHours(), time.getMinutes(), 0, 0);
  return merged;
}

export function isCause(value: unknown): value is CauseOfLossCode {
  return typeof value === 'object' && value !== null && 'code' in value;
}

/** FRS §11.2 searchable cause dropdown: valid only when a code has been picked from the list. */
export function causeSelectedValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value == null || value === '') {
    return { required: { message: 'Select a cause of loss.' } };
  }
  return isCause(value) ? null : { causeRequired: { message: MESSAGES.causeOfLossRequired } };
}

/** The optional initial reserve: component and amount are both given or both left empty. */
function requiredWhenSiblingSet(sibling: string, message: string) {
  return (control: AbstractControl): ValidationErrors | null => {
    const siblingValue = control.parent?.get(sibling)?.value;
    const siblingSet = siblingValue != null && siblingValue !== '';
    const selfEmpty = control.value == null || control.value === '';
    return siblingSet && selfEmpty ? { required: { message } } : null;
  };
}
