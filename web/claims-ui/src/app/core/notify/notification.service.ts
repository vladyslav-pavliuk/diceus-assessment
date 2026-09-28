import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

export type NotificationSeverity = 'success' | 'info' | 'warn' | 'error';

const DURATION_MS: Record<NotificationSeverity, number> = {
  success: 5000,
  info: 5000,
  warn: 7000,
  error: 9000,
};

/** Snackbars with a severity (FRS §11.4 "snackbar notifications with appropriate severity"). */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  show(message: string, severity: NotificationSeverity = 'info', action = 'Dismiss') {
    return this.snackBar.open(message, action, {
      duration: DURATION_MS[severity],
      panelClass: ['app-snack', `app-snack--${severity}`],
      politeness: severity === 'error' || severity === 'warn' ? 'assertive' : 'polite',
    });
  }

  success(message: string, action?: string) {
    return this.show(message, 'success', action);
  }

  info(message: string) {
    return this.show(message, 'info');
  }

  warn(message: string) {
    return this.show(message, 'warn');
  }

  error(message: string) {
    return this.show(message, 'error');
  }
}
