import { Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, finalize, switchMap } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { ApiError } from '../../../core/http/api-error';
import {
  ClaimDetail,
  ClaimParty,
  PartyInput,
  RiskObjectInput,
} from '../../../core/models/claim.models';
import { enumLabel } from '../../../core/models/enums';
import { NotificationService } from '../../../core/notify/notification.service';
import { PartyForm } from '../../../shared/forms/party-form';
import { RiskObjectForm } from '../../../shared/forms/risk-object-form';
import { Badge } from '../../../shared/ui/badge';
import { confirm } from '../../../shared/ui/confirm-dialog';
import { ErrorSummary } from '../../../shared/ui/error-summary';
import { ClaimDetailStore } from '../claim-detail.store';

export const LAST_CLAIMANT_MESSAGE = 'The last active Claimant cannot be removed.';

/**
 * Whether a party's Remove button is enabled: active parties only, and never the last active Claimant
 * (FRS §11.3 "disabled for last Claimant"; PTY-01, which the API enforces with a 422).
 */
export function canRemoveParty(party: ClaimParty, parties: readonly ClaimParty[]): boolean {
  if (!party.isActive) {
    return false;
  }
  if (party.partyRole !== 'Claimant') {
    return true;
  }
  return (
    parties.filter((candidate) => candidate.isActive && candidate.partyRole === 'Claimant').length >
    1
  );
}

/**
 * Tab 2 (FRS §11.3): every party with role badge, contact details and active/inactive state; inline Add
 * Party; soft Remove (IsActive = false, D-27). Risk objects are listed and added here too (D-40 Q1).
 */
@Component({
  selector: 'app-parties-tab',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    PartyForm,
    RiskObjectForm,
    Badge,
    ErrorSummary,
  ],
  templateUrl: './parties-tab.html',
  styleUrl: './tabs.scss',
})
export class PartiesTab {
  private readonly api = inject(ClaimsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  protected readonly store = inject(ClaimDetailStore);

  readonly claim = input.required<ClaimDetail>();

  private readonly partyForm = viewChild(PartyForm);
  private readonly riskObjectForm = viewChild(RiskObjectForm);

  protected readonly label = enumLabel;
  protected readonly lastClaimantMessage = LAST_CLAIMANT_MESSAGE;
  protected readonly addingParty = signal(false);
  protected readonly addingRiskObject = signal(false);
  protected readonly savingParty = signal(false);
  protected readonly savingRiskObject = signal(false);
  protected readonly removing = signal<string | null>(null);
  protected readonly partyErrors = signal<string[]>([]);
  protected readonly riskObjectErrors = signal<string[]>([]);

  /** Active parties first, Claimants first among them. */
  protected readonly parties = computed(() =>
    [...this.claim().parties].sort(
      (a, b) =>
        Number(b.isActive) - Number(a.isActive) ||
        Number(b.partyRole === 'Claimant') - Number(a.partyRole === 'Claimant'),
    ),
  );

  protected canRemove(party: ClaimParty): boolean {
    return !this.store.isReadOnly() && canRemoveParty(party, this.claim().parties);
  }

  protected removeTooltip(party: ClaimParty): string {
    if (!party.isActive) {
      return 'Already removed';
    }
    return this.canRemove(party) ? 'Remove from claim' : this.lastClaimantMessage;
  }

  protected addParty(party: PartyInput): void {
    this.savingParty.set(true);
    this.partyErrors.set([]);
    this.api
      .addParty(this.claim().id, party)
      .pipe(finalize(() => this.savingParty.set(false)))
      .subscribe({
        next: (added) => {
          this.notifications.success(
            `${added.displayName} added as ${enumLabel(added.partyRole)}.`,
          );
          this.partyForm()?.reset();
          this.addingParty.set(false);
          this.store.reloadClaim();
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && error.isValidation) {
            this.partyErrors.set(
              this.partyForm()?.showServerErrors(error.errors) ?? error.messages,
            );
          }
          this.store.handleError(error);
        },
      });
  }

  protected removeParty(party: ClaimParty): void {
    confirm(this.dialog, {
      title: 'Remove party?',
      message: `${party.displayName} (${enumLabel(party.partyRole)}) will be marked inactive on this claim. The history is kept.`,
      confirmText: 'Remove',
    })
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.removing.set(party.id);
          return this.api
            .removeParty(this.claim().id, party.id)
            .pipe(finalize(() => this.removing.set(null)));
        }),
      )
      .subscribe({
        next: () => {
          this.notifications.success(`${party.displayName} removed from the claim.`);
          this.store.reloadClaim();
        },
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  protected addRiskObject(riskObject: RiskObjectInput): void {
    this.savingRiskObject.set(true);
    this.riskObjectErrors.set([]);
    this.api
      .addRiskObject(this.claim().id, riskObject)
      .pipe(finalize(() => this.savingRiskObject.set(false)))
      .subscribe({
        next: () => {
          this.notifications.success('Risk object added.');
          this.riskObjectForm()?.reset();
          this.addingRiskObject.set(false);
          this.store.reloadClaim();
        },
        error: (error: unknown) => {
          if (error instanceof ApiError && error.isValidation) {
            this.riskObjectErrors.set(
              this.riskObjectForm()?.showServerErrors(error.errors) ?? error.messages,
            );
          }
          this.store.handleError(error);
        },
      });
  }
}
