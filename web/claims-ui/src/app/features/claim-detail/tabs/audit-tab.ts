import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { RouterLink } from '@angular/router';
import { ClaimsApiService } from '../../../core/api/claims-api.service';
import { AuditEntry } from '../../../core/models/claim.models';
import { PagedResult } from '../../../core/models/reference.models';
import { Badge } from '../../../shared/ui/badge';
import { eventTypeTone } from '../../../shared/ui/badge-tones';
import { EmptyState } from '../../../shared/ui/empty-state';

export const AUDIT_PAGE_SIZE = 50;

/** The tab that shows an audit entry's related entity (ClaimAuditTrail's RelatedEntityType names). */
export function relatedTab(relatedEntityType: string | null): string | null {
  switch (relatedEntityType) {
    case 'ReserveTransaction':
      return 'reserves';
    case 'ClaimDocument':
      return 'documents';
    case 'ClaimParty':
    case 'ClaimRiskObject':
      return 'parties';
    case 'ClaimValidationIssue':
    case 'Policy':
      return 'overview';
    default:
      return null;
  }
}

/**
 * Tab 5 (FRS §11.3): the append-only audit log, newest first, 50 per page (D-33). Read-only: nothing
 * here can edit or delete an entry. A null user is the system actor (D-33).
 */
@Component({
  selector: 'app-audit-tab',
  imports: [
    DatePipe,
    RouterLink,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    Badge,
    EmptyState,
  ],
  templateUrl: './audit-tab.html',
  styleUrls: ['./tabs.scss', './audit-tab.scss'],
})
export class AuditTab {
  private readonly api = inject(ClaimsApiService);

  readonly claimId = input.required<string>();
  /** The claim's last update: a change reloads the current page, so new entries appear. */
  readonly claimUpdatedAt = input<string | null>(null);

  protected readonly page = signal<PagedResult<AuditEntry> | null>(null);
  protected readonly pageIndex = signal(0);
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());
  protected readonly tone = eventTypeTone;
  protected readonly relatedTab = relatedTab;
  protected readonly pageSize = AUDIT_PAGE_SIZE;

  protected readonly entries = computed(() => this.page()?.items ?? []);

  constructor() {
    effect(() => {
      this.claimUpdatedAt();
      this.load(this.claimId(), this.pageIndex());
    });
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
  }

  protected toggle(entry: AuditEntry): void {
    const next = new Set(this.expanded());
    if (next.has(entry.id)) {
      next.delete(entry.id);
    } else {
      next.add(entry.id);
    }
    this.expanded.set(next);
  }

  protected pretty(json: string | null): string {
    if (!json) {
      return '—';
    }
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }

  private load(claimId: string, pageIndex: number): void {
    this.api
      .getAudit(claimId, pageIndex + 1, AUDIT_PAGE_SIZE)
      .subscribe((page) => this.page.set(page));
  }
}
