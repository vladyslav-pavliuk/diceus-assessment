import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, concat, map, of, switchMap, tap } from 'rxjs';
import { ClaimsApiService } from '../../core/api/claims-api.service';
import { ClaimSummary } from '../../core/models/claim.models';
import { PagedResult } from '../../core/models/reference.models';
import { Amount } from '../../shared/ui/amount';
import { EmptyState } from '../../shared/ui/empty-state';
import { StatusChip } from '../../shared/ui/status-chip';
import { ClaimFilters, ClaimsFilterBar } from './claims-filter-bar';
import { PAGE_SIZE_OPTIONS, paramsFromQuery, queryFromParams } from './claims-query';

type LoadState =
  | { status: 'loading' }
  | { status: 'loaded'; page: PagedResult<ClaimSummary> }
  | { status: 'failed' };

/**
 * The dashboard (FRS §11.1, brief §3.7.1): a server-paginated, filterable claims table. The URL is the
 * state: filters and paging are query parameters, and every change of them loads the matching page;
 * switchMap drops a slower, older response.
 */
@Component({
  selector: 'app-claims-list',
  imports: [
    DatePipe,
    RouterLink,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    ClaimsFilterBar,
    StatusChip,
    Amount,
    EmptyState,
  ],
  templateUrl: './claims-list.html',
  styleUrl: './claims-list.scss',
})
export class ClaimsList {
  private readonly api = inject(ClaimsApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly columns = [
    'claimNumber',
    'policyNumber',
    'clientName',
    'lossDate',
    'cause',
    'status',
    'handler',
    'totalReserves',
  ];
  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly skeletonRows = Array.from({ length: 6 }, (_, index) => index);

  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(queryFromParams)), {
    requireSync: true,
  });

  /** Emits to load the current page again (the "Try again" button). */
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  /** The last page that loaded, kept on screen (dimmed) while the next one loads. */
  private readonly lastLoaded = signal<PagedResult<ClaimSummary> | null>(null);

  private readonly state = toSignal(
    combineLatest([this.route.queryParamMap.pipe(map(queryFromParams)), this.reload$]).pipe(
      switchMap(([query]) =>
        concat(
          of<LoadState>({ status: 'loading' }),
          this.api.listClaims(query).pipe(
            tap((page) => this.lastLoaded.set(page)),
            map((page): LoadState => ({ status: 'loaded', page })),
            catchError(() => of<LoadState>({ status: 'failed' })),
          ),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as LoadState },
  );

  protected readonly page = computed(() => {
    const state = this.state();
    return state.status === 'loaded' ? state.page : this.lastLoaded();
  });
  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly failed = computed(() => this.state().status === 'failed');
  protected readonly firstLoad = computed(() => this.loading() && this.lastLoaded() === null);

  protected readonly filters = computed<ClaimFilters>(() => {
    const query = this.query();
    return {
      status: query.status,
      dateFrom: query.dateFrom,
      dateTo: query.dateTo,
      assignedHandlerId: query.assignedHandlerId,
      causeOfLossCode: query.causeOfLossCode,
      search: query.search,
    };
  });

  protected readonly hasFilters = computed(() => {
    const filters = this.filters();
    return Boolean(
      filters.status?.length ||
      filters.dateFrom ||
      filters.dateTo ||
      filters.assignedHandlerId ||
      filters.causeOfLossCode ||
      filters.search,
    );
  });

  protected onFiltersChange(filters: ClaimFilters): void {
    // A new filter starts again from the first page.
    this.navigate({ ...this.query(), ...filters, page: 1 });
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ ...this.query(), page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected clearFilters(): void {
    this.navigate({ page: 1, pageSize: this.query().pageSize });
  }

  protected retry(): void {
    this.reload$.next();
  }

  protected open(claim: ClaimSummary): void {
    void this.router.navigate(['/claims', claim.id]);
  }

  private navigate(query: Parameters<typeof paramsFromQuery>[0]): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: paramsFromQuery(query) });
  }
}
