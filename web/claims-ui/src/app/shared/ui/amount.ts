import { CurrencyPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

/** USD is the implicit currency (D-33). */
@Component({
  selector: 'app-amount',
  imports: [CurrencyPipe],
  templateUrl: './amount.html',
})
export class Amount {
  readonly value = input.required<number>();
  readonly signed = input(false);

  protected readonly prefix = computed(() => (this.signed() && this.value() > 0 ? '+' : ''));
}
