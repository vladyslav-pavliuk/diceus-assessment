import { Component, computed, input } from '@angular/core';
import { ClaimStatus, enumLabel } from '../../core/models/enums';
import { STATUS_COLOURS } from './badge-tones';

/** The colour-coded claim status (FRS §11.1), used by the list and the detail header. */
@Component({
  selector: 'app-status-chip',
  template: `<span class="status status--{{ colour() }}">{{ label() }}</span>`,
  styles: `
    .status {
      display: inline-flex;
      align-items: center;
      padding: 2px 10px;
      border-radius: 999px;
      font: var(--mat-sys-label-medium);
      white-space: nowrap;
    }
    .status--grey {
      background: var(--status-grey-bg);
      color: var(--status-grey-fg);
    }
    .status--blue {
      background: var(--status-blue-bg);
      color: var(--status-blue-fg);
    }
    .status--orange {
      background: var(--status-orange-bg);
      color: var(--status-orange-fg);
    }
    .status--purple {
      background: var(--status-purple-bg);
      color: var(--status-purple-fg);
    }
    .status--green {
      background: var(--status-green-bg);
      color: var(--status-green-fg);
    }
    .status--amber {
      background: var(--status-amber-bg);
      color: var(--status-amber-fg);
    }
    .status--dark-grey {
      background: var(--status-dark-grey-bg);
      color: var(--status-dark-grey-fg);
    }
  `,
})
export class StatusChip {
  readonly status = input.required<ClaimStatus>();

  protected readonly colour = computed(() => STATUS_COLOURS[this.status()]);
  protected readonly label = computed(() => enumLabel(this.status()));
}
