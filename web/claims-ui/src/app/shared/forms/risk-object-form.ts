import { Component, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { RiskObjectInput } from '../../core/models/claim.models';
import { ASSET_TYPES, AssetType } from '../../core/models/enums';
import { applyServerErrors } from './server-errors';
import { MESSAGES, errorMessage, requiredWithMessage } from './validators';

export type RiskObjectGroup = FormGroup<{
  assetType: FormControl<AssetType | null>;
  assetDescription: FormControl<string>;
  damageDescription: FormControl<string>;
  assetReference: FormControl<string>;
}>;

/** A damaged asset's controls (FRS §9.4, the API's column sizes). */
export function createRiskObjectGroup(value: Partial<RiskObjectInput> = {}): RiskObjectGroup {
  return new FormGroup({
    assetType: new FormControl<AssetType | null>(
      value.assetType ?? null,
      requiredWithMessage('Invalid asset type.'),
    ),
    assetDescription: new FormControl(value.assetDescription ?? '', {
      nonNullable: true,
      validators: [
        requiredWithMessage(MESSAGES.assetDescriptionRequired),
        Validators.maxLength(500),
      ],
    }),
    damageDescription: new FormControl(value.damageDescription ?? '', { nonNullable: true }),
    assetReference: new FormControl(value.assetReference ?? '', {
      nonNullable: true,
      validators: Validators.maxLength(255),
    }),
  });
}

/** The request body; the API makes the first risk object of a claim primary (D-33). */
export function toRiskObjectInput(group: RiskObjectGroup): RiskObjectInput {
  const value = group.getRawValue();
  return {
    assetType: value.assetType!,
    assetDescription: value.assetDescription.trim(),
    damageDescription: value.damageDescription.trim() || null,
    assetReference: value.assetReference.trim() || null,
    isPrimary: false,
  };
}

/** The inline "Add risk object" row (FRS §11.2 step 2; the Parties tab after intake, D-40 Q1). */
@Component({
  selector: 'app-risk-object-form',
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <form class="inline-form" [formGroup]="group" (ngSubmit)="submit()">
      <div class="inline-form__row">
        <mat-form-field class="inline-form__role">
          <mat-label>Asset type</mat-label>
          <mat-select formControlName="assetType" required>
            @for (type of assetTypes; track type) {
              <mat-option [value]="type">{{ type }}</mat-option>
            }
          </mat-select>
          <mat-error>{{ error(group.controls.assetType) }}</mat-error>
        </mat-form-field>
        <mat-form-field class="inline-form__wide">
          <mat-label>Asset description</mat-label>
          <input matInput formControlName="assetDescription" required autocomplete="off" />
          <mat-error>{{ error(group.controls.assetDescription) }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Reference number (optional)</mat-label>
          <input
            matInput
            formControlName="assetReference"
            autocomplete="off"
            placeholder="Registration, serial…"
          />
          <mat-error>{{ error(group.controls.assetReference) }}</mat-error>
        </mat-form-field>
      </div>
      <div class="inline-form__row">
        <mat-form-field class="inline-form__wide">
          <mat-label>Damage description</mat-label>
          <input matInput formControlName="damageDescription" autocomplete="off" />
        </mat-form-field>
        <div class="inline-form__actions">
          <button mat-button type="button" (click)="cancelled.emit()" [disabled]="pending()">
            Cancel
          </button>
          <button mat-flat-button type="submit" [disabled]="pending()">
            @if (pending()) {
              <mat-spinner diameter="18" />
            }
            {{ submitLabel() }}
          </button>
        </div>
      </div>
    </form>
  `,
  styleUrl: './inline-form.scss',
})
export class RiskObjectForm {
  readonly submitLabel = input('Add risk object');
  readonly pending = input(false);
  readonly submitted = output<RiskObjectInput>();
  readonly cancelled = output<void>();

  protected readonly assetTypes = ASSET_TYPES;
  protected readonly error = errorMessage;

  readonly group = createRiskObjectGroup();

  protected submit(): void {
    this.group.markAllAsTouched();
    if (this.group.valid) {
      this.submitted.emit(toRiskObjectInput(this.group));
    }
  }

  reset(): void {
    this.group.reset({
      assetType: null,
      assetDescription: '',
      damageDescription: '',
      assetReference: '',
    });
  }

  showServerErrors(errors: Readonly<Record<string, string[]>>): string[] {
    return applyServerErrors(this.group, errors)
      .filter((placement) => placement.path === null)
      .flatMap((placement) => placement.messages);
  }
}
