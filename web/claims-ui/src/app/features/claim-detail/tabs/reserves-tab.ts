import { DatePipe, PercentPipe } from '@angular/common';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, filter, finalize, switchMap } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { isAtLeast } from '../../../core/auth/roles';
import { ClaimDetail } from '../../../core/models/claim.models';
import { RESERVE_COMPONENTS, enumLabel } from '../../../core/models/enums';
import { ReserveHistoryEntry, ReserveSubmitted } from '../../../core/models/reserve.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { reserveRowActions } from '../../../shared/domain/reserve-actions';
import { Amount } from '../../../shared/ui/amount';
import { Badge } from '../../../shared/ui/badge';
import { APPROVAL_TONES, POSTING_TONES } from '../../../shared/ui/badge-tones';
import { confirm } from '../../../shared/ui/confirm-dialog';
import { EmptyState } from '../../../shared/ui/empty-state';
import { prompt } from '../../../shared/ui/prompt-dialog';
import { ClaimDetailStore } from '../claim-detail.store';
import { AddReservePanel } from './add-reserve-panel';

/**
 * Tab 3 (FRS §11.3): a summary card per component (current balance, pending amount in amber), the
 * transaction history with +/- colouring, role-gated Approve/Reject, Retract for the submitter, the GL
 * posting badge with Retry, and the slide-in Add Reserve panel. Also the BR-R-05 aggregate and, for a
 * manager, the reserve-limit override (D-08).
 */
@Component({
  selector: 'app-reserves-tab',
  imports: [
    DatePipe,
    PercentPipe,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatTooltipModule,
    MatSidenavModule,
    MatProgressBarModule,
    Amount,
    Badge,
    EmptyState,
    AddReservePanel,
  ],
  templateUrl: './reserves-tab.html',
  styleUrls: ['./tabs.scss', './reserves-tab.scss'],
})
export class ReservesTab implements OnInit {
  private readonly api = inject(ClaimsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  protected readonly store = inject(ClaimDetailStore);

  readonly claim = input.required<ClaimDetail>();

  protected readonly label = enumLabel;
  protected readonly columns = [
    'date',
    'type',
    'component',
    'amount',
    'status',
    'submittedBy',
    'approvedBy',
    'posting',
    'actions',
  ];

  protected readonly panelOpen = signal(false);
  /** The row whose command is in flight: its buttons are disabled meanwhile. */
  protected readonly busyRow = signal<string | null>(null);
  protected readonly savingOverride = signal(false);

  protected readonly reserves = this.store.reserves;

  /** One card per component in FRS §6.2 order; a component not opened yet shows as such. */
  protected readonly cards = computed(() => {
    const components = this.reserves()?.components ?? [];
    return RESERVE_COMPONENTS.map((type) => ({
      type,
      summary: components.find((component) => component.component === type) ?? null,
    }));
  });

  protected readonly aggregateShare = computed(() => {
    const reserves = this.reserves();
    return reserves && reserves.aggregateLimit > 0
      ? reserves.approvedAggregate / reserves.aggregateLimit
      : 0;
  });

  protected readonly isManager = computed(() =>
    isAtLeast(this.store.currentUser()?.role, 'Manager'),
  );

  ngOnInit(): void {
    this.store.reloadReserves();
  }

  // mat-table rows are untyped in the template, so the lookups take the row type here.
  protected approvalTone(row: ReserveHistoryEntry) {
    return APPROVAL_TONES[row.approvalStatus];
  }

  protected postingTone(row: ReserveHistoryEntry) {
    return POSTING_TONES[row.postingStatus];
  }

  protected actions(row: ReserveHistoryEntry) {
    return reserveRowActions(row, this.store.currentUser(), this.store.isReadOnly());
  }

  protected onSubmitted(result: ReserveSubmitted): void {
    this.panelOpen.set(false);
    const transaction = result.transaction;
    const verb = { Add: 'opened', Adjust: 'adjusted', Reverse: 'reversed' }[
      transaction.transactionType
    ];
    const what = `${enumLabel(result.component)} reserve ${verb}`;
    if (transaction.approvalStatus === 'AutoApproved') {
      this.notifications.success(`${what} and auto-approved.`);
    } else {
      this.notifications.info(`${what}: awaits ${transaction.requiredAuthority} approval.`);
    }
    for (const warning of result.warnings) {
      this.notifications.warn(warning);
    }
    this.store.afterReserveChange();
  }

  protected approve(row: ReserveHistoryEntry): void {
    confirm(this.dialog, {
      title: 'Approve reserve?',
      message: `${enumLabel(row.component)} ${row.transactionType.toLowerCase()} of ${formatUsd(row.amount)}, submitted by ${
        row.submittedByName ?? 'another user'
      }.`,
      confirmText: 'Approve',
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.run(row, this.api.approveReserve(this.claim().id, row.id))),
      )
      .subscribe({
        next: () =>
          this.notifications.success('Reserve approved. The GL posting runs in the background.'),
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  protected reject(row: ReserveHistoryEntry): void {
    prompt(this.dialog, {
      title: 'Reject reserve',
      message: `${enumLabel(row.component)} ${row.transactionType.toLowerCase()} of ${formatUsd(row.amount)}. The row stays in the history.`,
      label: 'Rejection reason',
      confirmText: 'Reject',
      maxLength: 500,
    })
      .pipe(
        switchMap((reason) =>
          this.run(row, this.api.rejectReserve(this.claim().id, row.id, reason)),
        ),
      )
      .subscribe({
        next: () => this.notifications.success('Reserve rejected.'),
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  protected retract(row: ReserveHistoryEntry): void {
    confirm(this.dialog, {
      title: 'Retract reserve?',
      message: 'Your pending transaction is cancelled. You can then submit a new one.',
      confirmText: 'Retract',
    })
      .pipe(
        filter(Boolean),
        switchMap(() => this.run(row, this.api.retractReserve(this.claim().id, row.id))),
      )
      .subscribe({
        next: () => this.notifications.success('Reserve retracted.'),
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  protected retryPosting(row: ReserveHistoryEntry): void {
    this.run(row, this.api.retryGlPosting(this.claim().id, row.id)).subscribe({
      next: () => this.notifications.info('GL posting queued again.'),
      error: (error: unknown) => this.store.handleError(error),
    });
  }

  protected toggleOverride(): void {
    const enable = !(this.reserves()?.reserveLimitOverride ?? false);
    prompt(this.dialog, {
      title: enable ? 'Allow reserves above $10,000,000' : 'Remove the reserve limit override',
      message: enable
        ? 'Approved reserves on this claim may then exceed the $10,000,000 aggregate limit (BR-R-05).'
        : 'Approvals will again be blocked above the $10,000,000 aggregate limit.',
      label: 'Reason',
      confirmText: enable ? 'Allow' : 'Remove override',
      maxLength: 500,
    })
      .pipe(
        switchMap((reason) => {
          this.savingOverride.set(true);
          return this.api
            .setReserveLimitOverride(this.claim().id, enable, reason)
            .pipe(finalize(() => this.savingOverride.set(false)));
        }),
      )
      .subscribe({
        next: () => {
          this.notifications.success(
            enable ? 'Reserve limit override set.' : 'Reserve limit override removed.',
          );
          this.store.afterReserveChange();
        },
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  /** Runs a row command with the row's buttons disabled, then reloads what it changed. */
  private run<T>(row: ReserveHistoryEntry, command: Observable<T>): Observable<T> {
    this.busyRow.set(row.id);
    return command.pipe(
      finalize(() => {
        this.busyRow.set(null);
        this.store.afterReserveChange();
      }),
    );
  }
}

function formatUsd(amount: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}
