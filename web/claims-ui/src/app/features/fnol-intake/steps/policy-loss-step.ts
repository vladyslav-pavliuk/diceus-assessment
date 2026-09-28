import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTimepickerModule } from '@angular/material/timepicker';
import { ReferenceApiService } from '../../../core/api/reference-api.service';
import { CLOCK } from '../../../core/clock';
import { CauseOfLossCode, Policy } from '../../../core/models/reference.models';
import { PolicyTypeahead } from '../../../shared/forms/policy-typeahead';
import {
  LOSS_DESCRIPTION_MIN_LENGTH,
  errorMessage,
  trimmedLength,
} from '../../../shared/forms/validators';
import { PolicyPeriodState } from '../../../shared/domain/policy-period';
import { FnolForm, isCause } from '../fnol-form';

/** FNOL step 1: policy & loss details (FRS §5.2, §11.2). */
@Component({
  selector: 'app-policy-loss-step',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatTimepickerModule,
    MatAutocompleteModule,
    MatSlideToggleModule,
    MatIconModule,
    PolicyTypeahead,
  ],
  templateUrl: './policy-loss-step.html',
  styleUrl: './steps.scss',
})
export class PolicyLossStep {
  private readonly reference = inject(ReferenceApiService);

  readonly group = input.required<FnolForm['controls']['policyLoss']>();
  /** The picked policy, or null (FnolIntake derives it from the form). */
  readonly policy = input<Policy | null>(null);
  readonly policyState = input<PolicyPeriodState | null>(null);
  readonly causeText = input<string>('');

  protected readonly error = errorMessage;
  protected readonly minLength = LOSS_DESCRIPTION_MIN_LENGTH;
  protected readonly trimmedLength = trimmedLength;
  /** The date picker greys out future days (BR-C-01); the validator still checks the exact time. */
  protected readonly maxDate = inject(CLOCK)();

  private readonly causeCodes = toSignal(this.reference.causeOfLossCodes(), {
    initialValue: [] as CauseOfLossCode[],
  });

  /** FRS §11.2 "searchable dropdown": the codes whose name, code or category contain the typed text. */
  protected readonly causeOptions = computed(() => {
    const text = this.causeText().trim().toLowerCase();
    return [...this.causeCodes()]
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .filter(
        (code) =>
          !text ||
          code.name.toLowerCase().includes(text) ||
          code.code.toLowerCase().includes(text) ||
          code.perilCategory.toLowerCase().includes(text),
      );
  });

  protected displayCause(value: CauseOfLossCode | string | null): string {
    return isCause(value) ? value.name : (value ?? '');
  }
}
