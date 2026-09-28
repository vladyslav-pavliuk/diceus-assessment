import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { ClaimDetail, ValidationIssue } from '../../../core/models/claim.models';
import { CLAIM_SEVERITIES, ClaimSeverity } from '../../../core/models/enums';
import { Policy } from '../../../core/models/reference.models';
import { NotificationService } from '../../../core/notify/notification.service';
import {
  PolicyTypeahead,
  isPolicy,
  policySelectedValidator,
} from '../../../shared/forms/policy-typeahead';
import { Badge } from '../../../shared/ui/badge';
import { ISSUE_SEVERITY_TONES, SEVERITY_TONES } from '../../../shared/ui/badge-tones';
import { prompt } from '../../../shared/ui/prompt-dialog';
import { ClaimDetailStore } from '../claim-detail.store';

/**
 * Also hosts Acknowledge (D-19) and Link policy (BR-C-06): without them an out-of-period or Unknown-policy claim could
 * never progress.
 */
@Component({
  selector: 'app-overview-tab',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    CurrencyPipe,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    Badge,
    PolicyTypeahead,
  ],
  templateUrl: './overview-tab.html',
  styleUrl: './tabs.scss',
})
export class OverviewTab {
  private readonly api = inject(ClaimsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  protected readonly store = inject(ClaimDetailStore);

  readonly claim = input.required<ClaimDetail>();

  protected readonly severities = CLAIM_SEVERITIES;
  protected readonly severityTones = SEVERITY_TONES;
  protected readonly issueTones = ISSUE_SEVERITY_TONES;

  // NVARCHAR(MAX) in the API, so no length limit.
  protected readonly notes = new FormControl('', { nonNullable: true });
  protected readonly severity = new FormControl<ClaimSeverity>('Standard', { nonNullable: true });
  protected readonly policy = new FormControl<Policy | string | null>(
    null,
    policySelectedValidator,
  );

  protected readonly savingNotes = signal(false);
  protected readonly savingSeverity = signal(false);
  protected readonly linking = signal(false);
  protected readonly changingPolicy = signal(false);
  protected readonly acknowledging = signal<string | null>(null);

  protected readonly openIssues = computed(() =>
    this.claim().validationIssues.filter((issue) => issue.status === 'Open'),
  );
  protected readonly closedIssues = computed(() =>
    this.claim().validationIssues.filter((issue) => issue.status !== 'Open'),
  );
  protected readonly notesChanged = signal(false);

  constructor() {
    // A reload shows the server's values; a local edit is kept until saved.
    effect(() => {
      const claim = this.claim();
      if (!this.notesChanged()) {
        this.notes.setValue(claim.notes ?? '', { emitEvent: false });
      }
      this.severity.setValue(claim.severity, { emitEvent: false });
      if (this.store.isReadOnly()) {
        this.notes.disable({ emitEvent: false });
        this.severity.disable({ emitEvent: false });
      } else {
        this.notes.enable({ emitEvent: false });
        this.severity.enable({ emitEvent: false });
      }
    });
    this.notes.valueChanges.subscribe(() => this.notesChanged.set(true));
  }

  protected saveNotes(): void {
    this.savingNotes.set(true);
    this.api
      .updateDetails(this.claim().id, { notes: this.notes.value, severity: null })
      .pipe(finalize(() => this.savingNotes.set(false)))
      .subscribe({
        next: () => {
          this.notesChanged.set(false);
          this.notifications.success('Notes saved.');
          this.store.reloadClaim();
        },
        error: (error: unknown) => this.store.handleError(error),
      });
  }

  protected discardNotes(): void {
    this.notesChanged.set(false);
    this.notes.setValue(this.claim().notes ?? '', { emitEvent: false });
  }

  protected saveSeverity(severity: ClaimSeverity): void {
    if (severity === this.claim().severity) {
      return;
    }
    this.savingSeverity.set(true);
    this.api
      .updateDetails(this.claim().id, { notes: null, severity })
      .pipe(finalize(() => this.savingSeverity.set(false)))
      .subscribe({
        next: () => {
          this.notifications.success(`Severity set to ${severity}.`);
          this.store.reloadClaim();
        },
        error: (error: unknown) => {
          this.severity.setValue(this.claim().severity, { emitEvent: false });
          this.store.handleError(error);
        },
      });
  }

  protected acknowledge(issue: ValidationIssue): void {
    prompt(this.dialog, {
      title: 'Acknowledge warning',
      message: issue.message,
      label: 'Acknowledgement note',
      confirmText: 'Acknowledge',
      maxLength: 500,
    }).subscribe((note) => {
      this.acknowledging.set(issue.id);
      this.api
        .acknowledgeIssue(this.claim().id, issue.id, note)
        .pipe(finalize(() => this.acknowledging.set(null)))
        .subscribe({
          next: () => {
            this.notifications.success('Warning acknowledged.');
            this.store.reloadClaim();
          },
          error: (error: unknown) => this.store.handleError(error),
        });
    });
  }

  protected linkPolicy(): void {
    this.policy.markAsTouched();
    const policy = this.policy.value;
    if (!isPolicy(policy)) {
      return;
    }
    this.linking.set(true);
    this.api
      .linkPolicy(this.claim().id, policy.id)
      .pipe(finalize(() => this.linking.set(false)))
      .subscribe({
        next: () => {
          this.notifications.success(`Policy ${policy.policyNumber} linked.`);
          this.policy.reset(null);
          this.changingPolicy.set(false);
          this.store.reloadClaim();
        },
        error: (error: unknown) => this.store.handleError(error),
      });
  }
}
