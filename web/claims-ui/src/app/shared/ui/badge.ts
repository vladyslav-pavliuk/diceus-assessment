import { Component, input } from '@angular/core';
import { BadgeTone } from './badge-tones';

/** A small coloured label: severity, approval status, GL posting status, audit event type, party role. */
@Component({
  selector: 'app-badge',
  templateUrl: './badge.html',
  styleUrl: './badge.scss',
})
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
}
