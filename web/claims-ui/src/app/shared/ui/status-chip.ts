import { Component, computed, input } from '@angular/core';
import { ClaimStatus, enumLabel } from '../../core/models/enums';
import { STATUS_COLOURS } from './badge-tones';

/** The colour-coded claim status (FRS §11.1), used by the list and the detail header. */
@Component({
  selector: 'app-status-chip',
  templateUrl: './status-chip.html',
  styleUrl: './status-chip.scss',
})
export class StatusChip {
  readonly status = input.required<ClaimStatus>();

  protected readonly colour = computed(() => STATUS_COLOURS[this.status()]);
  protected readonly label = computed(() => enumLabel(this.status()));
}
