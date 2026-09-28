import { CurrencyPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

/**
 * A USD amount (the implicit currency, D-33). With `signed`, increases are green with a "+" and
 * decreases red (FRS §11.3 history table).
 */
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
