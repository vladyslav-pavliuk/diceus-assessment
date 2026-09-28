import { PolicyPeriod, policyPeriodState } from './policy-period';

// The three intake Warnings the API records on a new Draft claim (FRS §5.4, §8; D-06). The FNOL form
// computes them before submitting, so it can ask for confirmation (FRS §11.2 "confirmation dialog if
// any Warning validation issues exist"). The messages are the API's, word for word.

export const INTAKE_WARNING_MESSAGES = {
  lossDateOutsidePolicyPeriod: 'Loss date is outside the policy effective period.',
  noPolicyLinked: 'No policy linked. Policy must be associated before reserves can be set.',
  noRiskObjects: 'No risk objects linked.',
} as const;

export interface IntakeWarningInput {
  /** The selected policy; null when none is selected or "Unknown policy" is on. */
  policy: PolicyPeriod | null;
  lossDate: Date | null;
  riskObjectCount: number;
}

export function intakeWarnings(input: IntakeWarningInput): string[] {
  const warnings: string[] = [];

  if (input.policy == null) {
    warnings.push(INTAKE_WARNING_MESSAGES.noPolicyLinked);
  } else if (policyPeriodState(input.policy, input.lossDate) === 'outside-period') {
    warnings.push(INTAKE_WARNING_MESSAGES.lossDateOutsidePolicyPeriod);
  }

  if (input.riskObjectCount === 0) {
    warnings.push(INTAKE_WARNING_MESSAGES.noRiskObjects);
  }

  return warnings;
}
