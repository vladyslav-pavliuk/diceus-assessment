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

@Injectable({ providedIn: 'root' })
export class ReferenceApiService {
  private readonly http = inject(HttpClient);

  // Fetched once and shared. A failed fetch is not cached, so the next subscriber tries again.
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

  /** At most 20 results (D-40). */
  searchPolicies(q: string): Observable<Policy[]> {
    return this.http.get<Policy[]>(apiUrl('policies', 'search'), {
      params: new HttpParams().set('q', q),
      context: new HttpContext().set(SKIP_GLOBAL_LOADING, true),
    });
  }

  policyCoverage(policyId: string): Observable<PolicyCoverage> {
    return this.http.get<PolicyCoverage>(apiUrl('policies', policyId, 'coverage'));
  }

  /** All roles when `role` is omitted (D-43). */
  users(role?: UserRole): Observable<User[]> {
    const params = role ? new HttpParams().set('role', role) : undefined;
    return this.http.get<User[]>(apiUrl('users'), { params });
  }
}
