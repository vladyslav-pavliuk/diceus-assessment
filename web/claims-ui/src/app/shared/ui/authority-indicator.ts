import { Component, computed, input } from '@angular/core';
import {
  AGGREGATE_LIMIT_WARNING,
  AUTHORITY_LABELS,
  AggregateContext,
  exceedsAggregateLimit,
  requiredAuthority,
} from '../domain/authority';

/** RSV-09. With the claim's aggregate context it also shows the BR-R-05 escalation to Manager. */
@Component({
  selector: 'app-authority-indicator',
  templateUrl: './authority-indicator.html',
  styleUrl: './authority-indicator.scss',
})
export class AuthorityIndicator {
  readonly amount = input<number | null>(null);
  /** Omitted at FNOL, where nothing is approved yet. */
  readonly aggregate = input<AggregateContext | null>(null);

  protected readonly labels = AUTHORITY_LABELS;
  protected readonly aggregateWarning = AGGREGATE_LIMIT_WARNING;
  protected readonly authority = computed(() => requiredAuthority(this.amount(), this.aggregate()));
  protected readonly escalated = computed(() =>
    exceedsAggregateLimit(this.amount(), this.aggregate()),
  );
}
