import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** A friendly message when a list has nothing to show (FRS §11.1 empty state). */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  template: `
    <div class="empty">
      <mat-icon class="empty__icon">{{ icon() }}</mat-icon>
      <div class="empty__title">{{ title() }}</div>
      @if (message()) {
        <div class="empty__message">{{ message() }}</div>
      }
      <div class="empty__actions"><ng-content /></div>
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 40px 16px;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
    }
    .empty__icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--mat-sys-outline);
    }
    .empty__title {
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface);
    }
    .empty__actions {
      margin-top: 8px;
    }
  `,
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>();
}
