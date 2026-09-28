import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** A friendly message when a list has nothing to show (FRS §11.1 empty state). */
@Component({
  selector: 'app-empty-state',
  imports: [MatIconModule],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.scss',
})
export class EmptyState {
  readonly icon = input('inbox');
  readonly title = input.required<string>();
  readonly message = input<string>();
}
