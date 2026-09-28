import { FormArray, FormControl, FormGroup } from '@angular/forms';
import { FNOL_ERROR_ROOTS, stepOfErrorKey } from '../../features/fnol-intake/fnol-form';
import { applyServerErrors, toControlPath } from './server-errors';

describe('UI-FNOL-14 server 422 errors mapped to controls (D-40 item 4)', () => {
  it('UI_FNOL_14_Keys_become_control_paths', () => {
    expect(toControlPath('LossDate')).toBe('lossDate');
    expect(toControlPath('Parties[0].FirstName')).toBe('parties.0.firstName');
    expect(toControlPath('Parties[0].FirstName', FNOL_ERROR_ROOTS)).toBe(
      'partiesRisk.parties.0.firstName',
    );
    expect(toControlPath('InitialReserve.Amount', FNOL_ERROR_ROOTS)).toBe('reserve.amount');
    expect(toControlPath('PolicyId', FNOL_ERROR_ROOTS)).toBe('policyLoss.policy');
  });

  it('UI_FNOL_14_Server_errors_mapped_to_controls and unknown keys reported back', () => {
    const form = new FormGroup({
      policyLoss: new FormGroup({ lossDate: new FormControl<Date | null>(null) }),
      partiesRisk: new FormGroup({
        parties: new FormArray([new FormGroup({ firstName: new FormControl('') })]),
      }),
    });

    const placements = applyServerErrors(
      form,
      {
        LossDate: ['Loss date cannot be in the future.'],
        'Parties[0].FirstName': ['First name and last name are required for a person.'],
        Claim: ['Something about the claim.'],
      },
      FNOL_ERROR_ROOTS,
    );

    const lossDate = form.get('policyLoss.lossDate')!;
    expect(lossDate.errors).toEqual({ server: 'Loss date cannot be in the future.' });
    expect(lossDate.touched).toBe(true);
    expect(form.get('partiesRisk.parties.0.firstName')!.hasError('server')).toBe(true);
    expect(placements.find((placement) => placement.key === 'Claim')!.path).toBeNull();
  });

  it('UI_FNOL_14_A_server_error_clears_when_the_value_changes', () => {
    const control = new FormControl('x');
    applyServerErrors(new FormGroup({ lossLocation: control }), { LossLocation: ['Too long.'] });
    expect(control.hasError('server')).toBe(true);
    control.setValue('y');
    expect(control.hasError('server')).toBe(false);
  });

  it('UI_FNOL_14_Each_key_is_shown_at_the_top_of_its_step', () => {
    expect(stepOfErrorKey('LossDate')).toBe(0);
    expect(stepOfErrorKey('CauseOfLossCode')).toBe(0);
    expect(stepOfErrorKey('Parties[1].Email')).toBe(1);
    expect(stepOfErrorKey('ClaimParties')).toBe(1);
    expect(stepOfErrorKey('InitialReserve.Amount')).toBe(2);
    expect(stepOfErrorKey('Unexpected')).toBe(2);
  });
});
