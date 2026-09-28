import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { ReferenceApiService } from '../../../core/api/reference-api.service';
import { ApiError } from '../../../core/http/api-error';
import { User } from '../../../core/models/reference.models';
import { ErrorSummary } from '../../../shared/ui/error-summary';

export interface AssignHandlerDialogData {
  claimId: string;
  currentHandlerId: string | null;
}

/** Reassigns the claim's handler: supervisors and managers only (D-18, PUT /claims/{id}/assignee). */
@Component({
  selector: 'app-assign-handler-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    ErrorSummary,
  ],
  template: `
    <h2 mat-dialog-title>Assign handler</h2>
    <mat-dialog-content>
      <app-error-summary [messages]="errors()" />
      <mat-form-field class="field">
        <mat-label>Handler</mat-label>
        <mat-select [formControl]="userId">
          @for (user of users(); track user.id) {
            <mat-option [value]="user.id">{{ user.displayName }} · {{ user.role }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        (click)="assign()"
        [disabled]="userId.invalid || userId.value === data.currentHandlerId || pending()"
      >
        Assign
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .field {
      width: 100%;
    }
  `,
})
export class AssignHandlerDialog {
  private readonly api = inject(ClaimsApiService);
  private readonly dialogRef = inject<MatDialogRef<AssignHandlerDialog, boolean>>(MatDialogRef);
  protected readonly data = inject<AssignHandlerDialogData>(MAT_DIALOG_DATA);

  protected readonly users = toSignal(inject(ReferenceApiService).users(), {
    initialValue: [] as User[],
  });
  protected readonly userId = new FormControl<string | null>(
    this.data.currentHandlerId,
    Validators.required,
  );
  protected readonly pending = signal(false);
  protected readonly errors = signal<string[]>([]);

  protected assign(): void {
    const userId = this.userId.value;
    if (!userId) {
      return;
    }
    this.pending.set(true);
    this.api
      .assignHandler(this.data.claimId, userId)
      .pipe(finalize(() => this.pending.set(false)))
      .subscribe({
        next: () => this.dialogRef.close(true),
        error: (error: unknown) => {
          if (error instanceof ApiError && error.isValidation) {
            this.errors.set(error.messages);
          }
        },
      });
  }
}
