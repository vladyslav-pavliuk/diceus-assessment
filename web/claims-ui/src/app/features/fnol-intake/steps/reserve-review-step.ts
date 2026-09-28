import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RESERVE_COMPONENTS, enumLabel } from '../../../core/models/enums';
import { CreateClaimRequest } from '../../../core/models/claim.models';
import { Policy } from '../../../core/models/reference.models';
import { partyDisplayName } from '../../../shared/forms/party-form';
import { errorMessage } from '../../../shared/forms/validators';
import { AuthorityIndicator } from '../../../shared/ui/authority-indicator';
import { FnolForm } from '../fnol-form';

@Component({
  selector: 'app-reserve-review-step',
  imports: [
    ReactiveFormsModule,
    CurrencyPipe,
    DatePipe,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    AuthorityIndicator,
  ],
  templateUrl: './reserve-review-step.html',
  styleUrl: './steps.scss',
})
export class ReserveReviewStep {
  readonly group = input.required<FnolForm['controls']['reserve']>();
  /** The review shows exactly what Create will submit. */
  readonly review = input.required<CreateClaimRequest>();
  readonly policy = input<Policy | null>(null);
  readonly causeName = input<string>('');
  readonly reserveAmount = input<number | null>(null);
  readonly warnings = input<readonly string[]>([]);

  protected readonly components = RESERVE_COMPONENTS;
  protected readonly label = enumLabel;
  protected readonly error = errorMessage;
  protected readonly displayName = partyDisplayName;
}
