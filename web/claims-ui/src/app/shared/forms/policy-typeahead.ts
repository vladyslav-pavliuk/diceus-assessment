import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  finalize,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { Policy } from '../../core/models/reference.models';
import { MESSAGES, errorMessage } from './validators';

/** The control holds the typed text while searching and the Policy once one is picked. */
export type PolicyControl = FormControl<Policy | string | null>;

export const MIN_POLICY_QUERY_LENGTH = 2;

/** Valid only when a policy has been picked from the list, not merely typed. */
export function policySelectedValidator(control: AbstractControl): ValidationErrors | null {
  return isPolicy(control.value) ? null : { policyRequired: { message: MESSAGES.policyRequired } };
}

export function isPolicy(value: unknown): value is Policy {
  return typeof value === 'object' && value !== null && 'policyNumber' in value;
}

/**
 * Policy typeahead (FRS §11.2 step 1): searches by policy number or client name as the user types,
 * debounced, and switchMap cancels the previous search so an older, slower answer never replaces a
 * newer one. Reused by "Link policy" on the claim detail (BR-C-06, D-08).
 */
@Component({
  selector: 'app-policy-typeahead',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <mat-form-field class="typeahead">
      <mat-label>{{ label() }}</mat-label>
      <mat-icon matPrefix>policy</mat-icon>
      <input
        matInput
        [formControl]="control()"
        [matAutocomplete]="auto"
        placeholder="Policy number or client name"
        autocomplete="off"
      />
      @if (searching()) {
        <mat-spinner matSuffix diameter="20" class="typeahead__spinner" />
      }
      <mat-autocomplete #auto [displayWith]="display" autoActiveFirstOption>
        @for (policy of options(); track policy.id) {
          <mat-option [value]="policy">
            <span class="typeahead__number">{{ policy.policyNumber }}</span>
            <span class="typeahead__client">{{ policy.clientName }}</span>
            <span class="muted typeahead__dates">
              {{ policy.effectiveDate | date: 'd MMM y' }} –
              {{ policy.expirationDate | date: 'd MMM y' }}
            </span>
          </mat-option>
        }
        @if (noMatches()) {
          <mat-option disabled>No policy matches “{{ lastQuery() }}”</mat-option>
        }
      </mat-autocomplete>
      <mat-error>{{ error(control()) }}</mat-error>
    </mat-form-field>
  `,
  styles: `
    .typeahead {
      width: 100%;
    }
    .typeahead__spinner {
      margin-right: 12px;
    }
    .typeahead__number {
      font-weight: 500;
      margin-right: 12px;
    }
    .typeahead__client {
      margin-right: 12px;
    }
    .typeahead__dates {
      font: var(--mat-sys-body-small);
    }
  `,
})
export class PolicyTypeahead implements OnInit {
  private readonly reference = inject(ReferenceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly control = input.required<PolicyControl>();
  readonly label = input('Policy');

  protected readonly options = signal<Policy[]>([]);
  protected readonly searching = signal(false);
  protected readonly noMatches = signal(false);
  protected readonly lastQuery = signal('');
  protected readonly error = errorMessage;

  ngOnInit(): void {
    this.control()
      .valueChanges.pipe(
        filter((value): value is string | null => !isPolicy(value)),
        map((value) => (value ?? '').trim()),
        debounceTime(300),
        distinctUntilChanged(),
        tap((query) => {
          this.lastQuery.set(query);
          this.noMatches.set(false);
        }),
        switchMap((query) => {
          if (query.length < MIN_POLICY_QUERY_LENGTH) {
            return of([]);
          }
          this.searching.set(true);
          return this.reference.searchPolicies(query).pipe(
            tap((policies) => this.noMatches.set(policies.length === 0)),
            catchError(() => of([])),
            finalize(() => this.searching.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((policies) => this.options.set(policies));
  }

  protected display(value: Policy | string | null): string {
    return isPolicy(value) ? `${value.policyNumber} · ${value.clientName}` : (value ?? '');
  }
}
