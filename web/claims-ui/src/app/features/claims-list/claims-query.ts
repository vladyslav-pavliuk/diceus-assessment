import { ParamMap, Params } from '@angular/router';
import { ClaimListQuery } from '../../core/models/claim.models';
import { CLAIM_STATUSES, ClaimStatus } from '../../core/models/enums';

// The list's filters and page live in the URL (shareable, survive a refresh, work with the back button).
// These two functions are the only translation between the URL and the API query (D-29, D-40 item 9).

export const DEFAULT_PAGE_SIZE = 25;
export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

export function queryFromParams(params: ParamMap): ClaimListQuery {
  return {
    status: params
      .getAll('status')
      .filter((value): value is ClaimStatus =>
        (CLAIM_STATUSES as readonly string[]).includes(value),
      ),
    dateFrom: params.get('dateFrom'),
    dateTo: params.get('dateTo'),
    assignedHandlerId: params.get('handler'),
    causeOfLossCode: params.get('cause'),
    search: params.get('q'),
    page: positiveInt(params.get('page'), 1),
    pageSize: PAGE_SIZE_OPTIONS.includes(positiveInt(params.get('pageSize'), DEFAULT_PAGE_SIZE))
      ? positiveInt(params.get('pageSize'), DEFAULT_PAGE_SIZE)
      : DEFAULT_PAGE_SIZE,
  };
}

/** URL params for a query; defaults are left out so a plain /claims stays plain. */
export function paramsFromQuery(query: ClaimListQuery): Params {
  return {
    status: query.status?.length ? query.status : null,
    dateFrom: query.dateFrom || null,
    dateTo: query.dateTo || null,
    handler: query.assignedHandlerId || null,
    cause: query.causeOfLossCode || null,
    q: query.search?.trim() || null,
    page: query.page > 1 ? query.page : null,
    pageSize: query.pageSize !== DEFAULT_PAGE_SIZE ? query.pageSize : null,
  };
}

/** A picked calendar date as 'yyyy-MM-dd' in the user's own calendar (the API's DateOnly). */
export function toDateOnly(date: Date | null | undefined): string | null {
  if (!date || Number.isNaN(date.getTime())) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** 'yyyy-MM-dd' → a local Date at midnight, for the date-range picker. */
export function fromDateOnly(value: string | null | undefined): Date | null {
  const match = value ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(value) : null;
  return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
}

function positiveInt(value: string | null, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}
