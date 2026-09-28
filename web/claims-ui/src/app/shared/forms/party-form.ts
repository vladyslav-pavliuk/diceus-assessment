import { Component, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { PartyInput } from '../../core/models/claim.models';
import { PARTY_ROLES, PARTY_TYPES, PartyRole, PartyType, enumLabel } from '../../core/models/enums';
import { applyServerErrors } from './server-errors';
import { MESSAGES, errorMessage, requiredForPartyType, requiredWithMessage } from './validators';

export type PartyGroup = FormGroup<{
  role: FormControl<PartyRole | null>;
  type: FormControl<PartyType>;
  firstName: FormControl<string>;
  lastName: FormControl<string>;
  companyName: FormControl<string>;
  email: FormControl<string>;
  phone: FormControl<string>;
  notes: FormControl<string>;
}>;

/** A party's controls with the FRS §9.3 rules (the API's column sizes, D-40 item 13). */
export function createPartyGroup(value: Partial<PartyInput> = {}): PartyGroup {
  const group: PartyGroup = new FormGroup({
    role: new FormControl<PartyRole | null>(
      value.role ?? null,
      requiredWithMessage('Invalid party role.'),
    ),
    type: new FormControl<PartyType>(value.type ?? 'Person', { nonNullable: true }),
    firstName: new FormControl(value.firstName ?? '', {
      nonNullable: true,
      validators: [requiredForPartyType('Person'), Validators.maxLength(100)],
    }),
    lastName: new FormControl(value.lastName ?? '', {
      nonNullable: true,
      validators: [requiredForPartyType('Person'), Validators.maxLength(100)],
    }),
    companyName: new FormControl(value.companyName ?? '', {
      nonNullable: true,
      validators: [requiredForPartyType('Company'), Validators.maxLength(255)],
    }),
    email: new FormControl(value.email ?? '', {
      nonNullable: true,
      validators: [Validators.email, Validators.maxLength(255)],
    }),
    phone: new FormControl(value.phone ?? '', {
      nonNullable: true,
      validators: Validators.maxLength(50),
    }),
    notes: new FormControl(value.notes ?? '', { nonNullable: true }),
  });

  // The name rules read the type: re-check them whenever it changes.
  group.controls.type.valueChanges.subscribe(() => {
    for (const name of ['firstName', 'lastName', 'companyName'] as const) {
      group.controls[name].updateValueAndValidity({ emitEvent: false });
    }
  });
  return group;
}

/** The request body for a valid party group: blank optional fields become null. */
export function toPartyInput(group: PartyGroup): PartyInput {
  const value = group.getRawValue();
  const person = value.type === 'Person';
  return {
    role: value.role!,
    type: value.type,
    firstName: person ? value.firstName.trim() : null,
    lastName: person ? value.lastName.trim() : null,
    companyName: person ? null : value.companyName.trim(),
    email: value.email.trim() || null,
    phone: value.phone.trim() || null,
    notes: value.notes.trim() || null,
  };
}

export function partyDisplayName(
  party: Pick<PartyInput, 'type' | 'firstName' | 'lastName' | 'companyName'>,
): string {
  return party.type === 'Company'
    ? (party.companyName ?? '')
    : [party.firstName, party.lastName].filter(Boolean).join(' ');
}

/**
 * The inline "Add party" row (FRS §11.2 step 2, §11.3 Tab 2). Emits a valid party; the parent adds it
 * to the FNOL list or sends it to the API, then calls reset().
 */
@Component({
  selector: 'app-party-form',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonToggleModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './party-form.html',
  styleUrl: './inline-form.scss',
})
export class PartyForm {
  readonly submitLabel = input('Add party');
  readonly pending = input(false);
  readonly submitted = output<PartyInput>();
  readonly cancelled = output<void>();

  protected readonly roles = PARTY_ROLES;
  protected readonly types = PARTY_TYPES;
  protected readonly label = enumLabel;
  protected readonly error = errorMessage;
  protected readonly messages = MESSAGES;

  readonly group = createPartyGroup();

  protected submit(): void {
    this.group.markAllAsTouched();
    if (this.group.valid) {
      this.submitted.emit(toPartyInput(this.group));
    }
  }

  reset(): void {
    this.group.reset({
      role: null,
      type: 'Person',
      firstName: '',
      lastName: '',
      companyName: '',
      email: '',
      phone: '',
      notes: '',
    });
  }

  /** Shows an "add party" 422 next to the fields (keys are top-level: FirstName, Email, …; D-40 item 4). */
  showServerErrors(errors: Readonly<Record<string, string[]>>): string[] {
    return applyServerErrors(this.group, errors, { Role: 'role', Type: 'type' })
      .filter((placement) => placement.path === null)
      .flatMap((placement) => placement.messages);
  }
}
