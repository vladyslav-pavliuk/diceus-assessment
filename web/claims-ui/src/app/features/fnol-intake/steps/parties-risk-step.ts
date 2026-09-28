import { Component, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PartyInput, RiskObjectInput } from '../../../core/models/claim.models';
import { enumLabel } from '../../../core/models/enums';
import { PartyForm, createPartyGroup, partyDisplayName } from '../../../shared/forms/party-form';
import { RiskObjectForm, createRiskObjectGroup } from '../../../shared/forms/risk-object-form';
import { MESSAGES } from '../../../shared/forms/validators';
import { INTAKE_WARNING_MESSAGES } from '../../../shared/domain/intake-warnings';
import { Badge } from '../../../shared/ui/badge';
import { FnolForm } from '../fnol-form';

/**
 * FNOL step 2: parties & risk objects (FRS §11.2). Each section adds items through an inline row and
 * shows them as cards with a remove button. The parties array requires a Claimant (its validator
 * blocks the step); risk objects are only advised.
 */
@Component({
  selector: 'app-parties-risk-step',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule, PartyForm, RiskObjectForm, Badge],
  templateUrl: './parties-risk-step.html',
  styleUrl: './steps.scss',
})
export class PartiesRiskStep {
  readonly group = input.required<FnolForm['controls']['partiesRisk']>();

  protected readonly addingParty = signal(true);
  protected readonly addingRiskObject = signal(false);
  protected readonly label = enumLabel;
  protected readonly displayName = partyDisplayName;
  protected readonly claimantMessage = MESSAGES.claimantRequired;
  protected readonly riskAdvisory = INTAKE_WARNING_MESSAGES.noRiskObjects;

  protected addParty(party: PartyInput, form: PartyForm): void {
    const parties = this.group().controls.parties;
    parties.push(createPartyGroup(party));
    parties.markAsTouched();
    form.reset();
    this.addingParty.set(false);
  }

  protected removeParty(index: number): void {
    const parties = this.group().controls.parties;
    parties.removeAt(index);
    parties.markAsTouched();
  }

  protected addRiskObject(riskObject: RiskObjectInput, form: RiskObjectForm): void {
    this.group().controls.riskObjects.push(createRiskObjectGroup(riskObject));
    form.reset();
    this.addingRiskObject.set(false);
  }

  protected removeRiskObject(index: number): void {
    this.group().controls.riskObjects.removeAt(index);
  }
}
