import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Observable, map } from 'rxjs';

export interface ConfirmDialogData {
  title: string;
  message?: string;
  items?: readonly string[];
  confirmText?: string;
  cancelText?: string;
  /** Shows the items as warnings (amber) rather than plain text. */
  warning?: boolean;
}

/** A yes/no confirmation. Resolves true only when the user presses the confirm button. */
@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      @if (data.items?.length) {
        <div
          class="banner"
          [class.banner--warning]="data.warning"
          [class.banner--info]="!data.warning"
        >
          <mat-icon>{{ data.warning ? 'warning_amber' : 'info' }}</mat-icon>
          <ul>
            @for (item of data.items; track $index) {
              <li>{{ item }}</li>
            }
          </ul>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">{{ data.cancelText ?? 'Cancel' }}</button>
      <button mat-flat-button [mat-dialog-close]="true" cdkFocusInitial>
        {{ data.confirmText ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

export function confirm(dialog: MatDialog, data: ConfirmDialogData): Observable<boolean> {
  return dialog
    .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data, width: '480px' })
    .afterClosed()
    .pipe(map((result) => result === true));
}
