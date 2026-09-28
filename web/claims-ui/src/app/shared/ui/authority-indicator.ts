import { Component, computed, input } from '@angular/core';
import {
  AGGREGATE_LIMIT_WARNING,
  AUTHORITY_LABELS,
  AggregateContext,
  exceedsAggregateLimit,
  requiredAuthority,
} from '../domain/authority';

/**
 * The real-time authority threshold indicator (FRS §11.2 step 3, §11.3 Add Reserve panel; RSV-09).
 * Shows nothing until there is an amount to judge. Given the claim's aggregate context (Add Reserve panel), it also
 * shows the BR-R-05 escalation to Manager and the FRS §8 warning.
 */
@Component({
  selector: 'app-authority-indicator',
  templateUrl: './authority-indicator.html',
  styleUrl: './authority-indicator.scss',
})
export class AuthorityIndicator {
  readonly amount = input<number | null>(null);
  /** BR-R-05 context of an existing claim; omitted at FNOL, where nothing is approved yet. */
  readonly aggregate = input<AggregateContext | null>(null);

  protected readonly labels = AUTHORITY_LABELS;
  protected readonly aggregateWarning = AGGREGATE_LIMIT_WARNING;
  protected readonly authority = computed(() => requiredAuthority(this.amount(), this.aggregate()));
  protected readonly escalated = computed(() =>
    exceedsAggregateLimit(this.amount(), this.aggregate()),
  );
}
