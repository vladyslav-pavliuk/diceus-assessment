import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { SKIP_GLOBAL_LOADING } from '../http/interceptors';
import { UserRole } from '../models/enums';
import {
  CauseOfLossCode,
  ClaimStatusDefinition,
  Policy,
  PolicyCoverage,
  User,
} from '../models/reference.models';
import { apiUrl } from './api-url';

/**
 * Reference data, policy lookup and users (FRS §10.3, D-08). One of the three classes allowed to use
 * HttpClient (FRS §11 "typed service layer"; enforced by ESLint).
 */
@Injectable({ providedIn: 'root' })
export class ReferenceApiService {
  private readonly http = inject(HttpClient);

  // Reference data does not change while the app runs: fetched once, shared by every screen.
  // A failed fetch is not cached, so the next subscriber tries again.
  private readonly causeCodes$ = this.http
    .get<CauseOfLossCode[]>(apiUrl('reference', 'cause-of-loss-codes'))
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  private readonly statuses$ = this.http
    .get<ClaimStatusDefinition[]>(apiUrl('reference', 'claim-statuses'))
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  causeOfLossCodes(): Observable<CauseOfLossCode[]> {
    return this.causeCodes$;
  }

  claimStatuses(): Observable<ClaimStatusDefinition[]> {
    return this.statuses$;
  }

  /** Typeahead search by policy number or client name (at most 20 results, D-40 item 10). */
  searchPolicies(q: string): Observable<Policy[]> {
    return this.http.get<Policy[]>(apiUrl('policies', 'search'), {
      params: new HttpParams().set('q', q),
      context: new HttpContext().set(SKIP_GLOBAL_LOADING, true),
    });
  }

  policyCoverage(policyId: string): Observable<PolicyCoverage> {
    return this.http.get<PolicyCoverage>(apiUrl('policies', policyId, 'coverage'));
  }

  /** Active users of the caller's organisation; all roles when `role` is omitted (D-43). */
  users(role?: UserRole): Observable<User[]> {
    const params = role ? new HttpParams().set('role', role) : undefined;
    return this.http.get<User[]>(apiUrl('users'), { params });
  }
}
