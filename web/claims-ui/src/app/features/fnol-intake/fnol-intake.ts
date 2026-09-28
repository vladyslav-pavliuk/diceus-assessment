import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { Router, RouterLink } from '@angular/router';
import { Observable, filter, finalize, map, of, startWith, switchMap } from 'rxjs';
import { ClaimsApiService } from '../../core/api/claims-api.service';
import { CLOCK } from '../../core/clock';
import { ApiError } from '../../core/http/api-error';
import { ClaimCreated } from '../../core/models/claim.models';
import { NotificationService } from '../../core/notify/notification.service';
import { intakeWarnings } from '../../shared/domain/intake-warnings';
import { policyPeriodState } from '../../shared/domain/policy-period';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { isPolicy } from '../../shared/forms/policy-typeahead';
import { toNumber } from '../../shared/forms/validators';
import { confirm } from '../../shared/ui/confirm-dialog';
import { ErrorSummary } from '../../shared/ui/error-summary';
import {
  FNOL_ERROR_ROOTS,
  createFnolForm,
  isCause,
  stepOfErrorKey,
  toCreateClaimRequest,
} from './fnol-form';
import { PartiesRiskStep } from './steps/parties-risk-step';
import { PolicyLossStep } from './steps/policy-loss-step';
import { ReserveReviewStep } from './steps/reserve-review-step';

/** Anything that depends on several fields is a computed signal over the form's events, so it updates as the user types. */
@Component({
  selector: 'app-fnol-intake',
  imports: [
    RouterLink,
    MatStepperModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ErrorSummary,
    PolicyLossStep,
    PartiesRiskStep,
    ReserveReviewStep,
  ],
  templateUrl: './fnol-intake.html',
  styleUrl: './fnol-intake.scss',
})
export class FnolIntake {
  private readonly api = inject(ClaimsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly form = createFnolForm(inject(CLOCK));
  private readonly stepper = viewChild.required(MatStepper);

  /** One per claim being logged, so a double submit replays instead of duplicating (D-24). */
  private readonly idempotencyKey = crypto.randomUUID();
  private created = false;

  protected readonly pending = signal(false);
  protected readonly stepErrors = signal<string[][]>([[], [], []]);

  // Includes a server error set on a control.
  private readonly formState = toSignal(
    this.form.events.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    {
      requireSync: true,
    },
  );

  protected readonly policy = computed(() => {
    const step1 = this.formState().policyLoss;
    return !step1.unknownPolicy && isPolicy(step1.policy) ? step1.policy : null;
  });
  protected readonly policyState = computed(() => {
    const policy = this.policy();
    return policy ? policyPeriodState(policy, this.formState().policyLoss.lossDate) : null;
  });
  protected readonly causeText = computed(() => {
    const cause = this.formState().policyLoss.causeOfLoss;
    return typeof cause === 'string' ? cause : '';
  });
  protected readonly causeName = computed(() => {
    const cause = this.formState().policyLoss.causeOfLoss;
    return isCause(cause) ? cause.name : '';
  });
  protected readonly reserveAmount = computed(() =>
    this.form.controls.reserve.enabled ? toNumber(this.formState().reserve.amount) : null,
  );
  /** FRS §11.2: confirm when any Warning would be recorded (D-06). */
  protected readonly warnings = computed(() =>
    intakeWarnings({
      policy: this.policy(),
      lossDate: this.formState().policyLoss.lossDate,
      riskObjectCount: this.formState().partiesRisk.riskObjects.length,
    }),
  );
  protected readonly review = computed(() => {
    this.formState();
    return toCreateClaimRequest(this.form);
  });
  protected readonly canCreate = computed(() => {
    this.formState();
    const { policyLoss, partiesRisk, reserve } = this.form.controls;
    return policyLoss.valid && partiesRisk.valid && (reserve.disabled || reserve.valid);
  });

  protected create(): void {
    if (!this.canCreate() || this.pending()) {
      this.form.markAllAsTouched();
      return;
    }

    const warnings = this.warnings();
    const confirmed$: Observable<boolean> =
      warnings.length > 0
        ? confirm(this.dialog, {
            title: 'Create the claim with warnings?',
            message: 'The claim will be created in Draft. These warnings are recorded on it:',
            items: warnings,
            warning: true,
            confirmText: 'Create claim',
          })
        : of(true);

    confirmed$
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.pending.set(true);
          this.stepErrors.set([[], [], []]);
          return this.api
            .createClaim(toCreateClaimRequest(this.form), this.idempotencyKey)
            .pipe(finalize(() => this.pending.set(false)));
        }),
      )
      .subscribe({
        next: (created) => this.onCreated(created),
        error: (error: unknown) => this.onError(error),
      });
  }

  /** For the route guard. */
  canLeave(): boolean | Observable<boolean> {
    const { parties, riskObjects } = this.form.controls.partiesRisk.controls;
    const started = this.form.dirty || parties.length > 0 || riskObjects.length > 0;
    if (this.created || !started) {
      return true;
    }
    return confirm(this.dialog, {
      title: 'Discard this claim?',
      message: 'The details entered so far have not been saved.',
      confirmText: 'Discard',
      cancelText: 'Keep editing',
    });
  }

  private onCreated(created: ClaimCreated): void {
    this.created = true;
    const pendingReserve = created.initialReserve?.transaction.approvalStatus === 'PendingApproval';
    const suffix = pendingReserve
      ? ` The initial reserve awaits ${created.initialReserve!.transaction.requiredAuthority} approval.`
      : '';
    this.notifications.success(`Claim ${created.claimNumber} created.${suffix}`);
    void this.router.navigate(['/claims', created.id]);
  }

  private onError(error: unknown): void {
    if (!(error instanceof ApiError) || !error.isValidation) {
      return; // Already reported by the error interceptor's snackbar.
    }

    applyServerErrors(this.form, error.errors, FNOL_ERROR_ROOTS);
    const byStep: string[][] = [[], [], []];
    for (const [key, messages] of Object.entries(error.errors)) {
      byStep[stepOfErrorKey(key)].push(...messages);
    }
    this.stepErrors.set(byStep);

    const firstStep = byStep.findIndex((messages) => messages.length > 0);
    if (firstStep >= 0) {
      this.stepper().selectedIndex = firstStep;
    }
  }
}
