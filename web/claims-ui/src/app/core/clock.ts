import { InjectionToken } from '@angular/core';

/** The front end's TimeProvider, so a test can pin "now". */
export const CLOCK = new InjectionToken<() => Date>('CLOCK', {
  providedIn: 'root',
  factory: () => () => new Date(),
});
