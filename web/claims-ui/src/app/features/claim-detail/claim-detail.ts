import { Component, computed, effect, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabChangeEvent, MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EmptyState } from '../../shared/ui/empty-state';
import { ClaimDetailStore } from './claim-detail.store';
import { ClaimHeader } from './header/claim-header';
import { AuditTab } from './tabs/audit-tab';
import { DocumentsTab } from './tabs/documents-tab';
import { OverviewTab } from './tabs/overview-tab';
import { PartiesTab } from './tabs/parties-tab';
import { ReservesTab } from './tabs/reserves-tab';

export const DETAIL_TABS = ['overview', 'parties', 'reserves', 'documents', 'audit'] as const;
export type DetailTab = (typeof DETAIL_TABS)[number];

/**
 * The claim detail (FRS §11.3, brief §3.7.3): an always-visible header and five tabs. The selected tab
 * is in the URL (?tab=reserves), so audit entries can link to it and a refresh keeps the place. The
 * Reserves, Documents and Audit tabs load their data the first time they are opened.
 */
@Component({
  selector: 'app-claim-detail',
  imports: [
    RouterLink,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    EmptyState,
    ClaimHeader,
    OverviewTab,
    PartiesTab,
    ReservesTab,
    DocumentsTab,
    AuditTab,
  ],
  providers: [ClaimDetailStore],
  templateUrl: './claim-detail.html',
  styleUrl: './claim-detail.scss',
})
export class ClaimDetailPage {
  protected readonly store = inject(ClaimDetailStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Route parameter :id. */
  readonly id = input.required<string>();
  /** Query parameter ?tab=. */
  readonly tab = input<string | undefined>();

  protected readonly tabIndex = computed(() => {
    const index = DETAIL_TABS.indexOf((this.tab() ?? 'overview') as DetailTab);
    return index < 0 ? 0 : index;
  });

  constructor() {
    effect(() => this.store.load(this.id()));
  }

  protected onTabChange(event: MatTabChangeEvent): void {
    const tab = DETAIL_TABS[event.index];
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: tab === 'overview' ? null : tab },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
