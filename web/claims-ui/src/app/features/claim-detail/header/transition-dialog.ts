import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { finalize } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { ApiError } from '../../../core/http/api-error';
import { ClaimDetail, ClaimStatusChanged } from '../../../core/models/claim.models';
import { enumLabel } from '../../../core/models/enums';
import { ClaimStatusTransition } from '../../../core/models/reference.models';
import { openReservesTotal, transitionPreflight } from '../../../shared/domain/transitions';
import { Amount } from '../../../shared/ui/amount';
import { ErrorSummary } from '../../../shared/ui/error-summary';

export interface TransitionDialogData {
  claim: ClaimDetail;
  transition: ClaimStatusTransition;
}

/** 'conflict' when someone else changed the claim first; the caller reloads. */
export type TransitionDialogResult = ClaimStatusChanged | 'conflict' | undefined;

/** Shows the pre-flight checklist for Closed and for opening a Draft, and asks for a reason where the row requires one. */
@Component({
  selector: 'app-transition-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ErrorSummary,
    Amount,
  ],
  templateUrl: './transition-dialog.html',
  styleUrl: './transition-dialog.scss',
})
export class TransitionDialog {
  private readonly api = inject(ClaimsApiService);
  private readonly dialogRef =
    inject<MatDialogRef<TransitionDialog, TransitionDialogResult>>(MatDialogRef);
  protected readonly data = inject<TransitionDialogData>(MAT_DIALOG_DATA);

  protected readonly target = this.data.transition.toStatus;
  protected readonly targetLabel = enumLabel(this.target);
  protected readonly checklist = transitionPreflight(this.data.claim, this.target);
  protected readonly blocked = this.checklist.some((item) => item.blocking && !item.satisfied);
  protected readonly openReserves = openReservesTotal(this.data.claim);
  /** CC-04 (D-26). */
  protected readonly needsJustification = this.target === 'Closed' && this.openReserves > 0;

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: this.data.transition.requiresReason
      ? [Validators.required, Validators.pattern(/\S/), Validators.maxLength(500)]
      : [Validators.maxLength(500)],
  });
  protected readonly justification = new FormControl('', {
    nonNullable: true,
    validators: this.needsJustification
      ? [Validators.required, Validators.pattern(/\S/), Validators.maxLength(500)]
      : [],
  });

  protected readonly pending = signal(false);
  protected readonly serverErrors = signal<string[]>([]);
  protected readonly reasonLabel = computed(() =>
    this.target === 'Closed' ? 'Closure reason' : 'Reason',
  );

  protected confirm(): void {
    this.reason.markAsTouched();
    this.justification.markAsTouched();
    if (this.blocked || this.reason.invalid || this.justification.invalid || this.pending()) {
      return;
    }

    this.pending.set(true);
    this.serverErrors.set([]);
    this.api
      .transitionStatus(this.data.claim.id, {
        targetStatus: this.target,
        reason: this.reason.value.trim() || null,
        justification: this.needsJustification ? this.justification.value.trim() : null,
      })
      .pipe(finalize(() => this.pending.set(false)))
      .subscribe({
        next: (result) => this.dialogRef.close(result),
        error: (error: unknown) => {
          if (error instanceof ApiError && error.status === 409) {
            this.dialogRef.close('conflict');
          } else if (error instanceof ApiError && error.isValidation) {
            this.serverErrors.set(error.messages);
          }
        },
      });
  }
}
