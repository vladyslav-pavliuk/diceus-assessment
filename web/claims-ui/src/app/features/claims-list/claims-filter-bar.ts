import {
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, merge } from 'rxjs';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { ClaimListQuery } from '../../core/models/claim.models';
import { CLAIM_STATUSES, ClaimStatus, enumLabel } from '../../core/models/enums';
import { CauseOfLossCode, User } from '../../core/models/reference.models';
import { fromDateOnly, toDateOnly } from './claims-query';

export type ClaimFilters = Pick<
  ClaimListQuery,
  'status' | 'dateFrom' | 'dateTo' | 'assignedHandlerId' | 'causeOfLossCode' | 'search'
>;

/** Emits the whole filter set whenever it changes. */
@Component({
  selector: 'app-claims-filter-bar',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: './claims-filter-bar.html',
  styleUrl: './claims-filter-bar.scss',
})
export class ClaimsFilterBar implements OnInit {
  private readonly reference = inject(ReferenceApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly value = input.required<ClaimFilters>();
  readonly filtersChange = output<ClaimFilters>();

  protected readonly statuses = CLAIM_STATUSES;
  protected readonly label = enumLabel;

  protected readonly form = new FormGroup({
    status: new FormControl<ClaimStatus[]>([], { nonNullable: true }),
    dateFrom: new FormControl<Date | null>(null),
    dateTo: new FormControl<Date | null>(null),
    handler: new FormControl<User | string | null>(null),
    cause: new FormControl<string | null>(null),
    search: new FormControl('', { nonNullable: true }),
  });

  protected readonly users = signal<User[]>([]);
  protected readonly causeGroups = signal<{ category: string; codes: CauseOfLossCode[] }[]>([]);

  private readonly handlerText = toSignal(this.form.controls.handler.valueChanges, {
    initialValue: null,
  });
  protected readonly handlerOptions = computed(() => {
    const typed = this.handlerText();
    const text = (typeof typed === 'string' ? typed : '').trim().toLowerCase();
    return this.users().filter((user) => !text || user.displayName.toLowerCase().includes(text));
  });

  protected readonly hasFilters = computed(() => {
    const value = this.value();
    return Boolean(
      value.status?.length ||
      value.dateFrom ||
      value.dateTo ||
      value.assignedHandlerId ||
      value.causeOfLossCode ||
      value.search,
    );
  });

  constructor() {
    // URL → form (first load, back/forward), without echoing the change back out.
    effect(() => this.patchForm(this.value(), this.users()));
  }

  ngOnInit(): void {
    // Every role: the creator of any role becomes the handler (D-18, D-43).
    this.reference.users().subscribe((users) => this.users.set(users));
    this.reference
      .causeOfLossCodes()
      .subscribe((codes) => this.causeGroups.set(groupByCategory(codes)));

    const controls = this.form.controls;
    merge(
      controls.status.valueChanges,
      controls.dateFrom.valueChanges,
      controls.dateTo.valueChanges,
      controls.cause.valueChanges,
      controls.search.valueChanges.pipe(debounceTime(350)),
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.emit());

    // A half-typed handler name does not filter; picking one (or clearing the field) does.
    controls.handler.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((handler) => {
      if (handler === null || handler === '' || typeof handler === 'object') {
        this.emit();
      }
    });
  }

  protected displayUser(user: User | string | null): string {
    return typeof user === 'object' && user ? user.displayName : (user ?? '');
  }

  protected clear(): void {
    this.form.reset(
      { status: [], dateFrom: null, dateTo: null, handler: null, cause: null, search: '' },
      { emitEvent: false },
    );
    this.emit();
  }

  private emit(): void {
    const value = this.form.getRawValue();
    this.filtersChange.emit({
      status: value.status,
      dateFrom: toDateOnly(value.dateFrom),
      dateTo: toDateOnly(value.dateTo),
      assignedHandlerId:
        typeof value.handler === 'object' && value.handler ? value.handler.id : null,
      causeOfLossCode: value.cause,
      search: value.search.trim() || null,
    });
  }

  private patchForm(value: ClaimFilters, users: User[]): void {
    const handler = value.assignedHandlerId
      ? (users.find((user) => user.id === value.assignedHandlerId) ?? null)
      : null;
    this.form.patchValue(
      {
        status: value.status ?? [],
        dateFrom: fromDateOnly(value.dateFrom),
        dateTo: fromDateOnly(value.dateTo),
        handler,
        cause: value.causeOfLossCode ?? null,
        search: value.search ?? '',
      },
      { emitEvent: false },
    );
  }
}

function groupByCategory(
  codes: CauseOfLossCode[],
): { category: string; codes: CauseOfLossCode[] }[] {
  const groups = new Map<string, CauseOfLossCode[]>();
  for (const code of [...codes].sort((a, b) => a.sortOrder - b.sortOrder)) {
    groups.set(code.perilCategory, [...(groups.get(code.perilCategory) ?? []), code]);
  }
  return [...groups].map(([category, grouped]) => ({ category, codes: grouped }));
}
