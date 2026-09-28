import { Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable, filter } from 'rxjs';

export interface PromptDialogData {
  title: string;
  message?: string;
  label: string;
  confirmText?: string;
  required?: boolean;
  maxLength?: number;
}

/**
 * Asks for one piece of text: a rejection reason, an acknowledgement note, an override reason. Resolves
 * with the trimmed text, or not at all when cancelled.
 */
@Component({
  selector: 'app-prompt-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      <mat-form-field class="prompt__field">
        <mat-label>{{ data.label }}</mat-label>
        <textarea matInput [formControl]="text" rows="3" cdkFocusInitial></textarea>
        @if (data.maxLength) {
          <mat-hint align="end">{{ text.value.length }} / {{ data.maxLength }}</mat-hint>
        }
        <mat-error>
          @if (text.hasError('maxlength')) {
            At most {{ data.maxLength }} characters.
          } @else {
            {{ data.label }} is required.
          }
        </mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button (click)="submit()">{{ data.confirmText ?? 'Confirm' }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt__field {
      width: 100%;
      min-width: 400px;
    }
  `,
})
export class PromptDialog {
  protected readonly data = inject<PromptDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<PromptDialog, string>>(MatDialogRef);

  protected readonly text = new FormControl('', {
    nonNullable: true,
    validators: [
      ...(this.data.required === false ? [] : [Validators.required, Validators.pattern(/\S/)]),
      ...(this.data.maxLength ? [Validators.maxLength(this.data.maxLength)] : []),
    ],
  });

  protected submit(): void {
    this.text.markAsTouched();
    if (this.text.valid) {
      this.dialogRef.close(this.text.value.trim());
    }
  }
}

export function prompt(dialog: MatDialog, data: PromptDialogData): Observable<string> {
  return dialog
    .open<PromptDialog, PromptDialogData, string>(PromptDialog, { data, width: '520px' })
    .afterClosed()
    .pipe(filter((value): value is string => value !== undefined));
}
