import { Component, input } from '@angular/core';
import { BadgeTone } from './badge-tones';

@Component({
  selector: 'app-badge',
  templateUrl: './badge.html',
  styleUrl: './badge.scss',
})
export class Badge {
  readonly tone = input<BadgeTone>('neutral');
}
