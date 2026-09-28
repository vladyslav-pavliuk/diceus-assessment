import { Injectable, computed, signal } from '@angular/core';

/** Counts API calls in flight; the shell shows a progress bar while any is running (FRS §11.4). */
@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly inFlight = signal(0);

  readonly busy = computed(() => this.inFlight() > 0);

  start(): void {
    this.inFlight.update((count) => count + 1);
  }

  stop(): void {
    this.inFlight.update((count) => Math.max(0, count - 1));
  }
}
