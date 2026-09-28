import { ClaimDetail } from '../../core/models/claim.models';
import { ClaimStatusDefinition } from '../../core/models/reference.models';
import { closureChecklist, nextTransitions, transitionPreflight } from './transitions';

// A slice of the seeded transition table (D-09).
const table: ClaimStatusDefinition[] = [
  {
    status: 'Open',
    transitions: [
      {
        fromStatus: 'Open',
        toStatus: 'UnderInvestigation',
        minimumRole: 'Handler',
        requiresReason: false,
        isSystemOnly: false,
      },
      {
        fromStatus: 'Open',
        toStatus: 'Closed',
        minimumRole: 'Handler',
        requiresReason: true,
        isSystemOnly: false,
      },
    ],
  },
  {
    status: 'Closed',
    transitions: [
      {
        fromStatus: 'Closed',
        toStatus: 'Reopened',
        minimumRole: 'Supervisor',
        requiresReason: true,
        isSystemOnly: false,
      },
    ],
  },
  {
    status: 'Reopened',
    transitions: [
      {
        fromStatus: 'Reopened',
        toStatus: 'Open',
        minimumRole: null,
        requiresReason: false,
        isSystemOnly: true,
      },
    ],
  },
];

function claim(overrides: Partial<ClaimDetail> = {}): ClaimDetail {
  return {
    id: 'c1',
    claimNumber: 'CLM-2026-0000001',
    status: 'Open',
    assignedHandlerId: 'u1',
    parties: [
      {
        id: 'p1',
        partyRole: 'Claimant',
        partyType: 'Person',
        firstName: 'Maria',
        lastName: 'Lopez',
        companyName: null,
        displayName: 'Maria Lopez',
        email: null,
        phone: null,
        notes: null,
        isActive: true,
      },
    ],
    validationIssues: [],
    reserveComponents: [],
    ...overrides,
  } as ClaimDetail;
}

describe('UI-DET-02 transition menu (D-09)', () => {
  it('UI_DET_02_Menu_shows_only_valid_next_statuses for the current status', () => {
    expect(
      nextTransitions(table, 'Open', 'Handler').map((transition) => transition.toStatus),
    ).toEqual(['UnderInvestigation', 'Closed']);
  });

  it('UI_DET_02_Reopen_is_hidden_from_a_handler_and_shown_to_a_supervisor (BR-ST-04)', () => {
    expect(nextTransitions(table, 'Closed', 'Handler')).toEqual([]);
    expect(
      nextTransitions(table, 'Closed', 'Supervisor').map((transition) => transition.toStatus),
    ).toEqual(['Reopened']);
    expect(nextTransitions(table, 'Closed', 'Manager')).toHaveLength(1);
  });

  it('UI_DET_02_System_only_rows_are_never_offered (Reopened → Open)', () => {
    expect(nextTransitions(table, 'Reopened', 'Manager')).toEqual([]);
  });
});

describe('CC-01..04 closure pre-flight checklist (BR-ST-03)', () => {
  const component = (currentAmount: number, hasPendingApproval = false) => ({
    id: 'r',
    component: 'Indemnity' as const,
    currentAmount,
    pendingAmount: 0,
    hasPendingApproval,
    status: 'Active' as const,
  });

  it('CC_All_conditions_satisfied_for_a_clean_claim', () => {
    expect(closureChecklist(claim()).every((item) => item.satisfied)).toBe(true);
  });

  it('CC_01_A_pending_reserve_blocks_closure', () => {
    const item = closureChecklist(claim({ reserveComponents: [component(0, true)] }))[0];
    expect(item).toMatchObject({ code: 'CC-01', satisfied: false, blocking: true });
  });

  it('CC_02_An_open_Critical_issue_blocks_closure but a Warning does not', () => {
    const issue = (severity: 'Critical' | 'Warning') => ({
      id: 'i',
      ruleCode: 'X',
      severity,
      field: 'f',
      message: 'm',
      status: 'Open' as const,
      raisedAt: '',
      resolvedAt: null,
      resolvedByUserId: null,
      resolutionNote: null,
    });
    expect(closureChecklist(claim({ validationIssues: [issue('Critical')] }))[1].satisfied).toBe(
      false,
    );
    expect(closureChecklist(claim({ validationIssues: [issue('Warning')] }))[1].satisfied).toBe(
      true,
    );
  });

  it('CC_03_An_inactive_Claimant_does_not_count', () => {
    const inactive = claim().parties.map((party) => ({ ...party, isActive: false }));
    expect(closureChecklist(claim({ parties: inactive }))[2]).toMatchObject({
      code: 'CC-03',
      satisfied: false,
    });
  });

  it('CC_04_Open_reserves_need_a_justification_but_do_not_block', () => {
    const item = closureChecklist(claim({ reserveComponents: [component(30_000)] }))[3];
    expect(item).toMatchObject({ code: 'CC-04', satisfied: false, blocking: false });
    expect(item.label).toContain('$30,000.00');
  });

  it('CC_04_A_negative_subrogation_balance_is_not_an_open_reserve', () => {
    const subrogation = { ...component(-5_000), component: 'SubrogationRecoverable' as const };
    expect(closureChecklist(claim({ reserveComponents: [subrogation] }))[3].satisfied).toBe(true);
  });

  it('BR_ST_02_Opening_a_Draft_lists_its_conditions; other transitions have none', () => {
    const draft = claim({ status: 'Draft', assignedHandlerId: null });
    const items = transitionPreflight(draft, 'Open');
    expect(items.find((item) => item.code === 'D-18')!.satisfied).toBe(false);
    expect(transitionPreflight(claim(), 'UnderInvestigation')).toEqual([]);
  });
});
