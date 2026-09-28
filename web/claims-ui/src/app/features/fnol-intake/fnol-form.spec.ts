import { createPartyGroup } from '../../shared/forms/party-form';
import { createFnolForm, toCreateClaimRequest, withTimeOfDay } from './fnol-form';

const NOW = new Date('2026-09-28T12:00:00Z');
const policy = {
  id: 'pol-1',
  policyNumber: 'POL-2025-002001',
  clientName: 'Coastal Builders Group',
  effectiveDate: '2025-03-01',
  expirationDate: '2027-02-28',
  status: 'Active' as const,
  coverageTypes: [],
};
const fire = { code: 'COL-FIRE', name: 'Fire', perilCategory: 'Property' as const, sortOrder: 1 };

function filledForm() {
  const form = createFnolForm(() => NOW);
  form.controls.policyLoss.patchValue({
    policy,
    lossDate: new Date('2026-09-01T12:30:00Z'),
    causeOfLoss: fire,
    lossDescription: 'Kitchen fire spread to the dining room overnight.',
  });
  form.controls.partiesRisk.controls.parties.push(
    createPartyGroup({ role: 'Claimant', type: 'Person', firstName: 'Maria', lastName: 'Lopez' }),
  );
  return form;
}

describe('UI-FNOL-01 one FormGroup per step', () => {
  it('UI_FNOL_01_Cannot_advance_with_invalid_step: an empty form has invalid steps 1 and 2', () => {
    const form = createFnolForm(() => NOW);
    expect(form.controls.policyLoss.valid).toBe(false);
    expect(form.controls.partiesRisk.valid).toBe(false);
    expect(form.controls.reserve.valid).toBe(true); // the initial reserve is optional
  });

  it('UI_FNOL_01_Each_step_validates_on_its_own', () => {
    const form = filledForm();
    expect(form.controls.policyLoss.valid).toBe(true);
    expect(form.controls.partiesRisk.valid).toBe(true);
  });
});

describe('UI-FNOL-04 Unknown policy toggle', () => {
  it('UI_FNOL_04_Unknown_policy_disables_policy_and_reserve', () => {
    const form = filledForm();
    form.controls.reserve.patchValue({ component: 'Indemnity', amount: 5000 });

    form.controls.policyLoss.controls.unknownPolicy.setValue(true);

    expect(form.controls.policyLoss.controls.policy.disabled).toBe(true);
    expect(form.controls.reserve.disabled).toBe(true);
    expect(form.controls.policyLoss.valid).toBe(true);
    const request = toCreateClaimRequest(form);
    expect(request.policyId).toBeNull();
    expect(request.initialReserve).toBeNull();
  });

  it('UI_FNOL_04_Switching_it_off_requires_a_policy_again', () => {
    const form = filledForm();
    form.controls.policyLoss.controls.unknownPolicy.setValue(true);
    form.controls.policyLoss.controls.unknownPolicy.setValue(false);
    expect(form.controls.policyLoss.controls.policy.hasError('policyRequired')).toBe(true);
    expect(form.controls.reserve.enabled).toBe(true);
  });
});

describe('UI-FNOL-11 initial reserve fields', () => {
  it('UI_FNOL_11_Component_and_amount_are_both_given_or_both_empty', () => {
    const reserve = filledForm().controls.reserve;
    reserve.controls.amount.setValue(25_000);
    expect(reserve.controls.component.hasError('required')).toBe(true);
    reserve.controls.component.setValue('Indemnity');
    expect(reserve.valid).toBe(true);
  });
});

describe('UI-FNOL-05 loss date and time pickers', () => {
  it('UI_FNOL_05_The_time_picker_sets_the_time_of_the_picked_day', () => {
    const form = createFnolForm(() => NOW);
    const { lossDate, lossTime } = form.controls.policyLoss.controls;
    lossDate.setValue(new Date(2026, 8, 1));
    lossTime.setValue(new Date(2000, 0, 1, 14, 30));
    expect(lossDate.value).toEqual(new Date(2026, 8, 1, 14, 30));

    // Picking another day keeps the time.
    lossDate.setValue(new Date(2026, 7, 20));
    expect(lossDate.value).toEqual(new Date(2026, 7, 20, 14, 30));
  });

  it('UI_FNOL_05_withTimeOfDay_leaves_the_day_alone_without_a_time', () => {
    const day = new Date(2026, 8, 1);
    expect(withTimeOfDay(day, null)).toBe(day);
    expect(withTimeOfDay(null, new Date())).toBeNull();
  });
});

describe('POST /api/claims request (CreateClaimCommand)', () => {
  it('FNOL_Request_mirrors_the_command', () => {
    const form = filledForm();
    form.controls.reserve.patchValue({ component: 'Indemnity', amount: 25_000 });

    expect(toCreateClaimRequest(form)).toEqual({
      policyId: 'pol-1',
      lossDate: '2026-09-01T12:30:00.000Z',
      lossDescription: 'Kitchen fire spread to the dining room overnight.',
      lossLocation: null,
      causeOfLossCode: 'COL-FIRE',
      estimatedLossAmount: null,
      policeReportNumber: null,
      severity: null,
      parties: [
        {
          role: 'Claimant',
          type: 'Person',
          firstName: 'Maria',
          lastName: 'Lopez',
          companyName: null,
          email: null,
          phone: null,
          notes: null,
        },
      ],
      riskObjects: [],
      initialReserve: { component: 'Indemnity', amount: 25_000, changeReason: null },
    });
  });
});
