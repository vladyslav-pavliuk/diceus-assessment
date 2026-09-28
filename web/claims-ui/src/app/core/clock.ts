import { InjectionToken } from '@angular/core';

/**
 * The front end's TimeProvider (CLAUDE.md rule 9): date rules such as "not in the future" read the
 * time through this token, so a test can pin "now" instead of racing the real clock.
 */
export const CLOCK = new InjectionToken<() => Date>('CLOCK', {
  providedIn: 'root',
  factory: () => () => new Date(),
});
