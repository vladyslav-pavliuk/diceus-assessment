import { Component, computed, input } from '@angular/core';
import { AUTHORITY_LABELS, requiredAuthority } from '../domain/authority';

/**
 * The real-time authority threshold indicator (FRS §11.2 step 3, §11.3 Add Reserve panel; RSV-09).
 * Shows nothing until there is an amount to judge.
 */
@Component({
  selector: 'app-authority-indicator',
  template: `
    @if (authority(); as tier) {
      <div
        class="authority"
        [class.authority--auto]="tier === 'Auto'"
        role="status"
        aria-live="polite"
      >
        {{ labels[tier] }}
      </div>
    }
  `,
  styles: `
    .authority {
      display: inline-flex;
      padding: 6px 12px;
      border-radius: 8px;
      font: var(--mat-sys-label-large);
      background: var(--app-warning-bg);
      color: var(--app-warning-fg);
      border: 1px solid var(--app-warning-border);
    }
    .authority--auto {
      background: var(--app-success-bg);
      color: var(--app-success-fg);
      border-color: transparent;
    }
  `,
})
export class AuthorityIndicator {
  readonly amount = input<number | null>(null);

  protected readonly labels = AUTHORITY_LABELS;
  protected readonly authority = computed(() => requiredAuthority(this.amount()));
}
