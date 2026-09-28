import { Clipboard } from '@angular/cdk/clipboard';
import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { isAtLeast } from '../../../core/auth/roles';
import { ClaimDetail } from '../../../core/models/claim.models';
import { enumLabel } from '../../../core/models/enums';
import { ClaimStatusTransition } from '../../../core/models/reference.models';
import { NotificationService } from '../../../core/notify/notification.service';
import { nextTransitions } from '../../../shared/domain/transitions';
import { Badge } from '../../../shared/ui/badge';
import { SEVERITY_TONES } from '../../../shared/ui/badge-tones';
import { StatusChip } from '../../../shared/ui/status-chip';
import { ClaimDetailStore } from '../claim-detail.store';
import { AssignHandlerDialog, AssignHandlerDialogData } from './assign-handler-dialog';
import {
  TransitionDialog,
  TransitionDialogData,
  TransitionDialogResult,
} from './transition-dialog';

/**
 * The always-visible header (FRS §11.3): copyable claim number, status chip, policy and client, loss
 * date and cause, assigned handler, and the transition menu with the valid next statuses (D-09).
 */
@Component({
  selector: 'app-claim-header',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    MatDividerModule,
    StatusChip,
    Badge,
  ],
  templateUrl: './claim-header.html',
  styleUrl: './claim-header.scss',
})
export class ClaimHeader {
  private readonly store = inject(ClaimDetailStore);
  private readonly dialog = inject(MatDialog);
  private readonly clipboard = inject(Clipboard);
  private readonly notifications = inject(NotificationService);

  readonly claim = input.required<ClaimDetail>();

  protected readonly label = enumLabel;
  protected readonly severityTones = SEVERITY_TONES;

  protected readonly transitions = computed(() =>
    nextTransitions(
      this.store.statuses(),
      this.claim().status,
      this.store.currentUser()?.role ?? null,
    ),
  );

  /** D-18: supervisors and managers (re)assign the handler; not on Closed/Withdrawn claims (D-26). */
  protected readonly canAssign = computed(
    () => isAtLeast(this.store.currentUser()?.role, 'Supervisor') && !this.store.isReadOnly(),
  );

  protected copyNumber(): void {
    if (this.clipboard.copy(this.claim().claimNumber)) {
      this.notifications.info(`Copied ${this.claim().claimNumber} to the clipboard.`);
    }
  }

  protected openTransition(transition: ClaimStatusTransition): void {
    this.dialog
      .open<TransitionDialog, TransitionDialogData, TransitionDialogResult>(TransitionDialog, {
        data: { claim: this.claim(), transition },
        width: '560px',
        autoFocus: 'first-tabbable',
      })
      .afterClosed()
      .subscribe((result) => {
        if (result === 'conflict') {
          this.store.reloadClaim();
          return;
        }
        if (!result) {
          return;
        }
        const reopened = result.previousStatus === 'Closed' && transition.toStatus === 'Reopened';
        this.notifications.success(
          reopened
            ? `Claim reopened. Its status is now ${enumLabel(result.status)}.`
            : `Status changed to ${enumLabel(result.status)}.`,
        );
        this.store.reloadClaim();
      });
  }

  protected openAssign(): void {
    this.dialog
      .open<AssignHandlerDialog, AssignHandlerDialogData, boolean>(AssignHandlerDialog, {
        data: { claimId: this.claim().id, currentHandlerId: this.claim().assignedHandlerId },
        width: '440px',
      })
      .afterClosed()
      .subscribe((assigned) => {
        if (assigned) {
          this.notifications.success('Handler assigned.');
          this.store.reloadClaim();
        }
      });
  }
}
