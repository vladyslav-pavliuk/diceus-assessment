import { HttpClient, HttpContext, HttpEvent, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AuditEntry,
  ClaimCreated,
  ClaimDetail,
  ClaimListQuery,
  ClaimParty,
  ClaimStatusChanged,
  ClaimSummary,
  CreateClaimRequest,
  PartyInput,
  RiskObject,
  RiskObjectInput,
  TransitionClaimStatusRequest,
  UpdateClaimDetailsRequest,
  ValidationIssue,
} from '../models/claim.models';
import { ClaimDocumentWithUrl, DocumentDownloadUrl } from '../models/document.models';
import { DocumentType } from '../models/enums';
import { PagedResult } from '../models/reference.models';
import {
  ClaimReserves,
  ReserveSubmitted,
  ReserveTransaction,
  SubmitReserveRequest,
} from '../models/reserve.models';
import { SKIP_GLOBAL_LOADING } from '../http/interceptors';
import { apiUrl } from './api-url';

/**
 * Every claims endpoint (FRS §10.1, §10.2, D-08, D-40 Q1). One of the three classes allowed to use
 * HttpClient; components never call HTTP directly (FRS §11; enforced by ESLint).
 *
 * Writes that create something take an optional Idempotency-Key (D-24): the caller makes one per form
 * attempt, so a double submit or a network retry replays the first response instead of creating a
 * second claim, reserve or document. The API releases the key after any non-2xx (D-40 item 12).
 */
@Injectable({ providedIn: 'root' })
export class ClaimsApiService {
  private readonly http = inject(HttpClient);

  // --- Claims ---------------------------------------------------------------------------------

  listClaims(query: ClaimListQuery): Observable<PagedResult<ClaimSummary>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    for (const status of query.status ?? []) {
      params = params.append('status', status);
    }
    params = setIfPresent(params, 'dateFrom', query.dateFrom);
    params = setIfPresent(params, 'dateTo', query.dateTo);
    params = setIfPresent(params, 'assignedHandlerId', query.assignedHandlerId);
    params = setIfPresent(params, 'causeOfLossCode', query.causeOfLossCode);
    params = setIfPresent(params, 'policyId', query.policyId);
    params = setIfPresent(params, 'search', query.search);
    return this.http.get<PagedResult<ClaimSummary>>(apiUrl('claims'), { params });
  }

  getClaim(claimId: string): Observable<ClaimDetail> {
    return this.http.get<ClaimDetail>(apiUrl('claims', claimId));
  }

  createClaim(request: CreateClaimRequest, idempotencyKey?: string): Observable<ClaimCreated> {
    return this.http.post<ClaimCreated>(apiUrl('claims'), request, {
      headers: idempotencyHeaders(idempotencyKey),
    });
  }

  transitionStatus(
    claimId: string,
    request: TransitionClaimStatusRequest,
  ): Observable<ClaimStatusChanged> {
    return this.http.put<ClaimStatusChanged>(apiUrl('claims', claimId, 'status'), request);
  }

  getAudit(claimId: string, page: number, pageSize: number): Observable<PagedResult<AuditEntry>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<AuditEntry>>(apiUrl('claims', claimId, 'audit'), { params });
  }

  linkPolicy(claimId: string, policyId: string): Observable<void> {
    return this.http.put<void>(apiUrl('claims', claimId, 'policy'), { policyId });
  }

  assignHandler(claimId: string, userId: string): Observable<void> {
    return this.http.put<void>(apiUrl('claims', claimId, 'assignee'), { userId });
  }

  updateDetails(claimId: string, request: UpdateClaimDetailsRequest): Observable<void> {
    return this.http.patch<void>(apiUrl('claims', claimId), request);
  }

  setReserveLimitOverride(claimId: string, enabled: boolean, reason: string): Observable<void> {
    return this.http.put<void>(apiUrl('claims', claimId, 'reserve-limit-override'), {
      enabled,
      reason,
    });
  }

  // --- Parties, risk objects, validation issues ------------------------------------------------

  addParty(claimId: string, party: PartyInput): Observable<ClaimParty> {
    return this.http.post<ClaimParty>(apiUrl('claims', claimId, 'parties'), party);
  }

  removeParty(claimId: string, partyId: string): Observable<void> {
    return this.http.delete<void>(apiUrl('claims', claimId, 'parties', partyId));
  }

  addRiskObject(claimId: string, riskObject: RiskObjectInput): Observable<RiskObject> {
    return this.http.post<RiskObject>(apiUrl('claims', claimId, 'risk-objects'), riskObject);
  }

  acknowledgeIssue(claimId: string, issueId: string, note: string): Observable<ValidationIssue> {
    return this.http.post<ValidationIssue>(
      apiUrl('claims', claimId, 'validation-issues', issueId, 'acknowledge'),
      { note },
    );
  }

  // --- Reserves --------------------------------------------------------------------------------

  /** `background`: a poll the user did not ask for, so it does not drive the global progress bar. */
  getReserves(claimId: string, options: { background?: boolean } = {}): Observable<ClaimReserves> {
    return this.http.get<ClaimReserves>(apiUrl('claims', claimId, 'reserves'), {
      context: new HttpContext().set(SKIP_GLOBAL_LOADING, options.background ?? false),
    });
  }

  submitReserve(
    claimId: string,
    request: SubmitReserveRequest,
    idempotencyKey?: string,
  ): Observable<ReserveSubmitted> {
    return this.http.post<ReserveSubmitted>(apiUrl('claims', claimId, 'reserves'), request, {
      headers: idempotencyHeaders(idempotencyKey),
    });
  }

  approveReserve(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      apiUrl('claims', claimId, 'reserves', transactionId, 'approve'),
      {},
    );
  }

  rejectReserve(
    claimId: string,
    transactionId: string,
    rejectionReason: string,
  ): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      apiUrl('claims', claimId, 'reserves', transactionId, 'reject'),
      {
        rejectionReason,
      },
    );
  }

  retractReserve(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      apiUrl('claims', claimId, 'reserves', transactionId, 'retract'),
      {},
    );
  }

  retryGlPosting(claimId: string, transactionId: string): Observable<ReserveTransaction> {
    return this.http.post<ReserveTransaction>(
      apiUrl('claims', claimId, 'reserves', transactionId, 'retry-posting'),
      {},
    );
  }

  // --- Documents -------------------------------------------------------------------------------

  listDocuments(claimId: string): Observable<ClaimDocumentWithUrl[]> {
    return this.http.get<ClaimDocumentWithUrl[]>(apiUrl('claims', claimId, 'documents'));
  }

  /** Multipart upload with progress events (FRS §11.3 Tab 4 "shows progress indicator"). */
  uploadDocument(
    claimId: string,
    file: File,
    documentType: DocumentType | null,
    notes: string | null,
    idempotencyKey?: string,
  ): Observable<HttpEvent<ClaimDocumentWithUrl>> {
    const form = new FormData();
    form.append('file', file, file.name);
    if (documentType) {
      form.append('documentType', documentType);
    }
    if (notes) {
      form.append('notes', notes);
    }
    return this.http.post<ClaimDocumentWithUrl>(apiUrl('claims', claimId, 'documents'), form, {
      headers: idempotencyHeaders(idempotencyKey),
      reportProgress: true,
      observe: 'events',
    });
  }

  getDocumentUrl(claimId: string, documentId: string): Observable<DocumentDownloadUrl> {
    return this.http.get<DocumentDownloadUrl>(
      apiUrl('claims', claimId, 'documents', documentId, 'url'),
    );
  }
}

function setIfPresent(
  params: HttpParams,
  name: string,
  value: string | null | undefined,
): HttpParams {
  return value ? params.set(name, value) : params;
}

function idempotencyHeaders(key: string | undefined): HttpHeaders | undefined {
  return key ? new HttpHeaders({ 'Idempotency-Key': key }) : undefined;
}
