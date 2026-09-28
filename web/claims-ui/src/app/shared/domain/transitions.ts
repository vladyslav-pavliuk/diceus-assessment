import { isAtLeast } from '../../core/auth/roles';
import { ClaimDetail } from '../../core/models/claim.models';
import { ClaimStatus, UserRole } from '../../core/models/enums';
import { ClaimStatusDefinition, ClaimStatusTransition } from '../../core/models/reference.models';

/**
 * The statuses the header's transition menu offers (FRS §11.3): the rows of the ClaimStatusTransitions
 * table (D-09) out of the current status that a user may request. System-only rows (Reopened → Open)
 * are applied by the API itself, and rows above the user's role are hidden (the API would answer 403).
 */
export function nextTransitions(
  definitions: readonly ClaimStatusDefinition[],
  current: ClaimStatus,
  role: UserRole | null,
): ClaimStatusTransition[] {
  const definition = definitions.find((candidate) => candidate.status === current);
  return (definition?.transitions ?? []).filter(
    (transition) =>
      transition.fromStatus === current &&
      !transition.isSystemOnly &&
      (transition.minimumRole == null || isAtLeast(role, transition.minimumRole)),
  );
}

export interface PreflightItem {
  code: string;
  label: string;
  satisfied: boolean;
  /** False for CC-04: open reserves only need a justification note, they do not block. */
  blocking: boolean;
}

/**
 * The pre-flight checklist of the transition dialog, computed from the loaded claim. It mirrors the
 * checks of Claim.Status.cs so the user sees what will fail before asking; the API still decides.
 * - Closed: FRS §4.3 CC-01..04 (BR-ST-03).
 * - Open (from Draft): BR-ST-02 plus D-18 (handler) and D-19 (acknowledged BR-C-02).
 */
export function transitionPreflight(claim: ClaimDetail, target: ClaimStatus): PreflightItem[] {
  if (target === 'Closed') {
    return closureChecklist(claim);
  }
  if (target === 'Open' && claim.status === 'Draft') {
    return openChecklist(claim);
  }
  return [];
}

export function closureChecklist(claim: ClaimDetail): PreflightItem[] {
  const openReserveTotal = openReservesTotal(claim);
  return [
    {
      code: 'CC-01',
      label: 'No reserve transaction is pending approval',
      satisfied: !claim.reserveComponents.some((component) => component.hasPendingApproval),
      blocking: true,
    },
    {
      code: 'CC-02',
      label: 'No unresolved Critical validation issue',
      satisfied: !hasOpenCritical(claim),
      blocking: true,
    },
    {
      code: 'CC-03',
      label: 'At least one active Claimant party',
      satisfied: hasActiveClaimant(claim),
      blocking: true,
    },
    {
      code: 'CC-04',
      label:
        openReserveTotal > 0
          ? `Open reserves of ${formatUsd(openReserveTotal)} remain: a justification note is required`
          : 'No open reserves',
      satisfied: openReserveTotal <= 0,
      blocking: false,
    },
  ];
}

function openChecklist(claim: ClaimDetail): PreflightItem[] {
  return [
    {
      code: 'BR-ST-02',
      label: 'At least one active Claimant party',
      satisfied: hasActiveClaimant(claim),
      blocking: true,
    },
    {
      code: 'BR-ST-02',
      label: 'No unresolved Critical validation issue',
      satisfied: !claim.validationIssues.some(
        (issue) =>
          issue.severity === 'Critical' && issue.status === 'Open' && issue.ruleCode !== 'BR-C-03',
      ),
      blocking: true,
    },
    {
      code: 'BR-C-02',
      label: 'Loss-date warning resolved or acknowledged',
      satisfied: !claim.validationIssues.some(
        (issue) => issue.ruleCode === 'BR-C-02' && issue.status === 'Open',
      ),
      blocking: true,
    },
    {
      code: 'D-18',
      label: 'A handler is assigned',
      satisfied: claim.assignedHandlerId != null,
      blocking: true,
    },
  ];
}

/** Mirrors Claim.OpenReserveTotal: the sum of the positive component balances. */
export function openReservesTotal(claim: ClaimDetail): number {
  return claim.reserveComponents
    .filter((component) => component.currentAmount > 0)
    .reduce((sum, component) => sum + component.currentAmount, 0);
}

export function hasActiveClaimant(claim: ClaimDetail): boolean {
  return claim.parties.some((party) => party.isActive && party.partyRole === 'Claimant');
}

function hasOpenCritical(claim: ClaimDetail): boolean {
  return claim.validationIssues.some(
    (issue) => issue.severity === 'Critical' && issue.status === 'Open',
  );
}

function formatUsd(amount: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}
