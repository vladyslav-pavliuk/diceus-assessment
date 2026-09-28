import { AbstractControl } from '@angular/forms';

// Puts a 422's messages next to the controls they belong to (FRS §11.2 "display validation errors
// inline and at the top of the relevant step"). The API keys each error by the request's property
// path (D-40 item 4): `LossDate`, `Parties[0].FirstName`, `InitialReserve.Amount`. The form's controls
// use the same names in camelCase, so a key maps to a control path once its first segment is placed.

export interface ServerErrorPlacement {
  key: string;
  messages: string[];
  /** The control path the key resolved to, or null when the form has no such control. */
  path: string | null;
}

/**
 * Converts an API error key into a control path: `Parties[0].FirstName` → `parties.0.firstName`.
 * `roots` replaces the key's first segment with a path inside the form, for controls that live in a
 * step group or have a different name: with `{ Parties: 'partiesRisk.parties', PolicyId: 'policyLoss.policy' }`,
 * `Parties[0].FirstName` → `partiesRisk.parties.0.firstName` and `PolicyId` → `policyLoss.policy`.
 */
export function toControlPath(key: string, roots: Readonly<Record<string, string>> = {}): string {
  const [first, ...rest] = key
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .filter((segment) => segment.length > 0);
  if (first === undefined) {
    return '';
  }

  const head = roots[first] ?? camelCase(first);
  return [head, ...rest.map(camelCase)].join('.');
}

/**
 * Sets a `server` error on every control a key resolves to and marks it touched, so mat-error shows it.
 * Angular replaces the error the next time the control's value changes. Returns every key with its
 * messages, resolved or not, so the caller can also list them at the top of the step.
 */
export function applyServerErrors(
  root: AbstractControl,
  errors: Readonly<Record<string, string[]>>,
  roots: Readonly<Record<string, string>> = {},
): ServerErrorPlacement[] {
  return Object.entries(errors).map(([key, messages]) => {
    const path = toControlPath(key, roots);
    const control = path ? root.get(path) : null;
    if (!control) {
      return { key, messages, path: null };
    }

    control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
    control.markAsTouched();
    return { key, messages, path };
  });
}

function camelCase(segment: string): string {
  return /^\d+$/.test(segment) ? segment : segment.charAt(0).toLowerCase() + segment.slice(1);
}
