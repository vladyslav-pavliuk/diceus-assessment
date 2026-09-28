import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ClaimsApiService } from '../../core/api/claims-api.service';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { ClaimSummary } from '../../core/models/claim.models';
import { PagedResult } from '../../core/models/reference.models';
import { ClaimsList } from './claims-list';
import { DEFAULT_PAGE_SIZE, paramsFromQuery, queryFromParams } from './claims-query';

describe('UI-LIST-03 filters live in the URL (D-29)', () => {
  it('UI_LIST_03_Filters_map_to_query_params and back', () => {
    const query = {
      status: ['Open' as const, 'Draft' as const],
      dateFrom: '2026-01-01',
      dateTo: '2026-09-30',
      assignedHandlerId: 'user-1',
      causeOfLossCode: 'COL-FIRE',
      search: 'Coastal',
      page: 3,
      pageSize: 50,
    };
    const params = paramsFromQuery(query);
    expect(params).toEqual({
      status: ['Open', 'Draft'],
      dateFrom: '2026-01-01',
      dateTo: '2026-09-30',
      handler: 'user-1',
      cause: 'COL-FIRE',
      q: 'Coastal',
      page: 3,
      pageSize: 50,
    });
    expect(queryFromParams(convertToParamMap(params))).toEqual(query);
  });

  it('UI_LIST_03_Defaults_stay_out_of_the_URL', () => {
    expect(paramsFromQuery({ page: 1, pageSize: DEFAULT_PAGE_SIZE, status: [] })).toEqual({
      status: null,
      dateFrom: null,
      dateTo: null,
      handler: null,
      cause: null,
      q: null,
      page: null,
      pageSize: null,
    });
  });

  it('UI_LIST_03_Unknown_statuses_and_bad_paging_in_the_URL_are_ignored', () => {
    const query = queryFromParams(
      convertToParamMap({ status: ['Open', 'Bogus'], page: '-2', pageSize: '7' }),
    );
    expect(query.status).toEqual(['Open']);
    expect(query.page).toBe(1);
    expect(query.pageSize).toBe(DEFAULT_PAGE_SIZE);
  });
});

describe('UI-LIST claims table', () => {
  const claim: ClaimSummary = {
    id: 'c1',
    claimNumber: 'CLM-2026-0000001',
    policyId: 'p1',
    policyNumber: 'POL-2025-002001',
    clientName: 'Coastal Builders Group',
    lossDate: '2026-09-01T12:30:00Z',
    causeOfLossCode: 'COL-FIRE',
    causeOfLossName: 'Fire',
    status: 'Open',
    severity: 'Standard',
    assignedHandlerId: 'u1',
    assignedHandlerName: 'Alex Carter',
    totalReserves: 30_000,
    isSlaBreached: false,
    reportedDate: '2026-09-28T10:00:00Z',
  };

  function render(page: PagedResult<ClaimSummary>) {
    TestBed.configureTestingModule({
      imports: [ClaimsList],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideNativeDateAdapter(),
        { provide: ClaimsApiService, useValue: { listClaims: () => of(page) } },
        {
          provide: ReferenceApiService,
          useValue: { users: () => of([]), causeOfLossCodes: () => of([]) },
        },
      ],
    });
    const fixture = TestBed.createComponent(ClaimsList);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('UI_LIST_01_Columns_rendered in FRS §11.1 order, with the status badge', () => {
    const element = render({ items: [claim], totalCount: 1, page: 1, pageSize: 25 });
    const headers = [...element.querySelectorAll('th')].map((cell) => cell.textContent!.trim());
    expect(headers).toEqual([
      'Claim number',
      'Policy number',
      'Client name',
      'Loss date',
      'Cause of loss',
      'Status',
      'Handler',
      'Total reserves',
    ]);
    const cells = [...element.querySelectorAll('td')].map((cell) => cell.textContent!.trim());
    expect(cells).toContain('CLM-2026-0000001');
    expect(cells).toContain('$30,000.00');
    expect(element.querySelector('.status--blue')!.textContent!.trim()).toBe('Open');
  });

  it('UI_LIST_07_Empty_state_shown when nothing exists yet', () => {
    const element = render({ items: [], totalCount: 0, page: 1, pageSize: 25 });
    expect(element.querySelector('table')).toBeNull();
    expect(element.textContent).toContain('No claims yet');
  });
});
