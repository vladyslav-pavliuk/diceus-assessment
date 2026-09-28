import { AbstractControl } from '@angular/forms';

// The API keys each 422 error by request property path (D-40). Controls use the same names in camelCase, so a key
// maps to a control path once its first segment is placed.

export interface ServerErrorPlacement {
  key: string;
  messages: string[];
  /** Null when the form has no such control. */
  path: string | null;
}

/**
 * `Parties[0].FirstName` → `parties.0.firstName`. `roots` remaps the first segment for controls inside a step group,
 * e.g. `{ Parties: 'partiesRisk.parties' }` gives `partiesRisk.parties.0.firstName`.
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
 * Marks the controls touched so mat-error shows the message. Returns every key, resolved or not, so the caller can
 * also list them at the top of the step.
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
