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
  templateUrl: './assign-handler-dialog.html',
  styleUrl: './assign-handler-dialog.scss',
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
