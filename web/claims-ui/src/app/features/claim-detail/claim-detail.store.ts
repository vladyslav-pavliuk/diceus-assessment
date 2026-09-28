import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Subscription, catchError, map, of, timer } from 'rxjs';
import { ClaimsApiService } from '../../core/api/claims-api.service';
import { ReferenceApiService } from '../../core/api/reference-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { ApiError } from '../../core/http/api-error';
import { ClaimDetail } from '../../core/models/claim.models';
import { ClaimDocumentWithUrl } from '../../core/models/document.models';
import { ClaimStatusDefinition } from '../../core/models/reference.models';
import { ClaimReserves } from '../../core/models/reserve.models';
import { CurrentUser, isReadOnlyStatus } from '../../shared/domain/reserve-actions';

const POSTING_POLL_INTERVAL_MS = 3000;
const POSTING_POLL_ATTEMPTS = 10;

/**
 * Provided by the page component, because a route-level provider would outlive the page. The server is the source of
 * truth: every change is a command followed by a reload of what it affects, never an optimistic local edit.
 */
@Injectable()
export class ClaimDetailStore {
  private readonly api = inject(ClaimsApiService);
  private readonly auth = inject(AuthService);

  readonly claimId = signal<string | null>(null);
  readonly claim = signal<ClaimDetail | null>(null);
  readonly reserves = signal<ClaimReserves | null>(null);
  readonly documents = signal<ClaimDocumentWithUrl[] | null>(null);
  readonly notFound = signal(false);
  readonly loadFailed = signal(false);

  readonly statuses = toSignal(
    inject(ReferenceApiService)
      .claimStatuses()
      .pipe(catchError(() => of([] as ClaimStatusDefinition[]))),
    { initialValue: [] as ClaimStatusDefinition[] },
  );

  readonly currentUser = computed<CurrentUser | null>(() => {
    const user = this.auth.user();
    return user ? { id: user.id, role: user.role } : null;
  });

  readonly isReadOnly = computed(() => {
    const claim = this.claim();
    return claim != null && isReadOnlyStatus(claim.status);
  });

  private postingPoll?: Subscription;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopPolling());
  }

  load(claimId: string): void {
    this.claimId.set(claimId);
    this.claim.set(null);
    this.reserves.set(null);
    this.documents.set(null);
    this.notFound.set(false);
    this.loadFailed.set(false);
    this.reloadClaim();
  }

  reloadClaim(): void {
    const id = this.claimId();
    if (!id) {
      return;
    }
    this.api.getClaim(id).subscribe({
      next: (claim) => this.claim.set(claim),
      error: (error: unknown) => {
        if (error instanceof ApiError && error.status === 404) {
          this.notFound.set(true);
        } else if (this.claim() === null) {
          this.loadFailed.set(true);
        }
      },
    });
  }

  reloadReserves(): void {
    const id = this.claimId();
    if (!id) {
      return;
    }
    this.api.getReserves(id).subscribe((reserves) => {
      this.reserves.set(reserves);
      this.watchPendingPostings();
    });
  }

  reloadDocuments(): void {
    const id = this.claimId();
    if (id) {
      this.api.listDocuments(id).subscribe((documents) => this.documents.set(documents));
    }
  }

  afterReserveChange(): void {
    this.reloadClaim();
    this.reloadReserves();
  }

  /** A 409 means someone else changed the claim first (D-41). */
  handleError(error: unknown): void {
    if (error instanceof ApiError && error.status === 409) {
      this.reloadClaim();
      if (this.reserves()) {
        this.reloadReserves();
      }
    }
  }

  /**
   * Polls while an approved row is still Pending, so the badge turns Posted without a refresh. Bounded, because the
   * job's retries take about two minutes at worst (D-41).
   */
  private watchPendingPostings(): void {
    const awaitingPosting = (reserves: ClaimReserves | null) =>
      reserves?.transactions.some(
        (row) =>
          (row.approvalStatus === 'Approved' || row.approvalStatus === 'AutoApproved') &&
          row.postingStatus === 'Pending',
      ) ?? false;

    if (!awaitingPosting(this.reserves()) || this.postingPoll) {
      return;
    }

    let attempts = 0;
    this.postingPoll = timer(POSTING_POLL_INTERVAL_MS, POSTING_POLL_INTERVAL_MS)
      .pipe(map(() => this.claimId()))
      .subscribe((id) => {
        attempts++;
        if (!id || attempts > POSTING_POLL_ATTEMPTS) {
          this.stopPolling();
          return;
        }
        this.api.getReserves(id, { background: true }).subscribe({
          next: (reserves) => {
            this.reserves.set(reserves);
            if (!awaitingPosting(reserves)) {
              this.stopPolling();
            }
          },
          error: () => this.stopPolling(),
        });
      });
  }

  stopPolling(): void {
    this.postingPoll?.unsubscribe();
    this.postingPoll = undefined;
  }
}
