import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize, map, startWith } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { ApiError } from '../../../core/http/api-error';
import { ClaimDetail } from '../../../core/models/claim.models';
import {
  RESERVE_COMPONENTS,
  ReserveComponentType,
  ReserveTransactionType,
  enumLabel,
} from '../../../core/models/enums';
import { ClaimReserves, ReserveSubmitted } from '../../../core/models/reserve.models';
import {
  balanceError,
  effectiveAmount,
  inferTransactionType,
  submissionBlocker,
} from '../../../shared/domain/reserve-submission';
import { isReadOnlyStatus } from '../../../shared/domain/reserve-actions';
import { applyServerErrors } from '../../../shared/forms/server-errors';
import {
  adjustmentAmountValidator,
  errorMessage,
  openingReserveAmountValidator,
  requiredWithMessage,
  toNumber,
} from '../../../shared/forms/validators';
import { Amount } from '../../../shared/ui/amount';
import { AuthorityIndicator } from '../../../shared/ui/authority-indicator';
import { ErrorSummary } from '../../../shared/ui/error-summary';

/**
 * The slide-in Add Reserve panel (FRS §11.3): component, amount, reason and the live authority indicator.
 * The transaction type is inferred as the API does (D-05): Add opens a component, Adjust changes it by a
 * signed amount, Reverse releases its whole balance.
 */
@Component({
  selector: 'app-add-reserve-panel',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuthorityIndicator,
    ErrorSummary,
    Amount,
  ],
  templateUrl: './add-reserve-panel.html',
  styleUrl: './add-reserve-panel.scss',
})
export class AddReservePanel {
  private readonly api = inject(ClaimsApiService);

  readonly claim = input.required<ClaimDetail>();
  readonly reserves = input.required<ClaimReserves>();
  readonly submitted = output<ReserveSubmitted>();
  readonly closed = output<void>();

  protected readonly components = RESERVE_COMPONENTS;
  protected readonly label = enumLabel;
  protected readonly error = errorMessage;

  /** One Idempotency-Key per submission attempt; renewed after a success (D-24). */
  private idempotencyKey = crypto.randomUUID();

  protected readonly form = new FormGroup({
    component: new FormControl<ReserveComponentType | null>(
      null,
      requiredWithMessage('Invalid reserve component type.'),
    ),
    mode: new FormControl<'Change' | 'Reverse'>('Change', { nonNullable: true }),
    amount: new FormControl<number | null>(null, [(control) => this.amountRule(control)]),
    changeReason: new FormControl('', {
      nonNullable: true,
      validators: [requiredWithMessage('A change reason is required.'), Validators.maxLength(500)],
    }),
  });

  protected readonly pending = signal(false);
  protected readonly serverErrors = signal<string[]>([]);

  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    {
      requireSync: true,
    },
  );

  protected readonly existing = computed(() => {
    const component = this.value().component;
    return component
      ? this.reserves().components.find((candidate) => candidate.component === component)
      : undefined;
  });
  protected readonly transactionType = computed<ReserveTransactionType>(() =>
    this.value().mode === 'Reverse' && this.existing()
      ? 'Reverse'
      : inferTransactionType(this.existing()),
  );
  protected readonly amount = computed(() =>
    effectiveAmount(this.transactionType(), toNumber(this.value().amount), this.existing()),
  );
  protected readonly projectedBalance = computed(() => {
    const amount = this.amount();
    return amount == null ? null : (this.existing()?.currentAmount ?? 0) + amount;
  });
  protected readonly blocker = computed(() => {
    const claim = this.claim();
    return submissionBlocker({
      hasPolicy: claim.policyId != null,
      readOnlyStatus: isReadOnlyStatus(claim.status) ? claim.status : null,
      existing: this.existing(),
    });
  });

  constructor() {
    // The amount rule depends on the component and the mode: re-check it when they change. Reverse
    // computes its own amount, so the amount field is off for it.
    effect(() => {
      const reverse = this.transactionType() === 'Reverse';
      const amount = this.form.controls.amount;
      if (reverse && amount.enabled) {
        amount.disable({ emitEvent: false });
      } else if (!reverse && amount.disabled) {
        amount.enable({ emitEvent: false });
      }
      amount.updateValueAndValidity({ emitEvent: false });
    });
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    if (this.form.invalid || this.blocker() || !value.component || this.pending()) {
      return;
    }

    const type = this.transactionType();
    this.pending.set(true);
    this.serverErrors.set([]);
    this.api
      .submitReserve(
        this.claim().id,
        {
          component: value.component,
          amount: type === 'Reverse' ? null : toNumber(value.amount),
          changeReason: value.changeReason.trim(),
          // Omitted for Add/Adjust: the API infers it the same way (D-05). Reverse must be explicit.
          transactionType: type === 'Reverse' ? 'Reverse' : null,
        },
        this.idempotencyKey,
      )
      .pipe(finalize(() => this.pending.set(false)))
      .subscribe({
        next: (result) => {
          this.idempotencyKey = crypto.randomUUID();
          this.form.reset({ component: null, mode: 'Change', amount: null, changeReason: '' });
          this.submitted.emit(result);
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && error.isValidation) {
            const unplaced = applyServerErrors(this.form, error.errors, {
              ReserveAmount: 'amount',
              ReserveComponent: 'component',
            }).filter((placement) => placement.path === null);
            this.serverErrors.set(unplaced.flatMap((placement) => placement.messages));
          }
        },
      });
  }

  private amountRule(control: AbstractControl): ValidationErrors | null {
    if (toNumber(control.value) == null) {
      return { required: { message: 'Enter the reserve amount.' } };
    }
    const component = this.form?.controls.component.value;
    const existing = component
      ? this.reserves().components.find((candidate) => candidate.component === component)
      : undefined;
    if (!existing) {
      return openingReserveAmountValidator(control);
    }
    const shape = adjustmentAmountValidator(control);
    if (shape) {
      return shape;
    }
    const balance = component ? balanceError(component, existing, toNumber(control.value)) : null;
    return balance ? { balance: { message: balance } } : null;
  }
}
