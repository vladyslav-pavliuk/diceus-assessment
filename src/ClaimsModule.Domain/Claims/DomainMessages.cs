using System.Globalization;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Domain.Claims;

/// <summary>
/// User-facing rule messages. The first group is FRS §8 verbatim and is asserted word for word in
/// tests; the rest fill gaps the FRS leaves open and are marked ASSUMPTION in DECISIONS.md.
/// </summary>
public static class DomainMessages
{
    // FRS §8, verbatim.
    public const string LossDateInFuture = "Loss date cannot be in the future.";
    public const string LossDateRequired = "Loss date is required.";
    public const string LossDateOutsidePolicyPeriod = "Loss date is outside the policy effective period.";
    public const string LossDescriptionTooShort = "Loss description is required and must be at least 20 characters.";
    public const string CauseOfLossCodeInvalid = "Cause of loss code is not recognised or is inactive.";
    public const string NoPolicyLinked = "No policy linked. Policy must be associated before reserves can be set.";
    public const string ClaimantRequired = "At least one Claimant party is required to open a claim.";
    public const string ReserveAmountNotPositive = "Reserve amount must be greater than zero.";
    public const string InvalidReserveComponent = "Invalid reserve component type.";
    public const string AggregateLimitExceeded = "Total reserves will exceed $10,000,000. Manager override required.";
    public const string NoApprovalAuthority = "Your role does not have authority to approve this reserve amount.";
    public const string SelfApprovalNotPermitted = "Self-approval is not permitted.";

    public static string TransitionNotPermitted(ClaimStatus from, ClaimStatus to) =>
        $"Transition from {from} to {to} is not permitted.";

    public static string ClosureConditionNotSatisfied(string condition) =>
        $"Claim cannot be closed — {condition} is not satisfied.";

    // Closure conditions (FRS §4.3), used in the §8 closure template above.
    public const string ConditionNoPendingReserves = "CC-01 (no reserve transaction is pending approval)";
    public const string ConditionNoOpenCriticalIssues = "CC-02 (no unresolved Critical validation issue)";
    public const string ConditionActiveClaimant = "CC-03 (at least one active Claimant party)";
    public const string ConditionOpenReservesJustified = "CC-04 (a justification note is required to close a claim with open reserves)";

    // ASSUMPTION wordings (D-05, D-18, D-19, D-22, D-25, D-26, D-39).
    public const string LossDateOutsidePolicyPeriodNotAcknowledged =
        "Loss date is outside the policy effective period. Acknowledge the warning or link a policy that covers the loss date before opening the claim.";
    public const string HandlerRequiredToOpen = "A handler must be assigned before the claim can be opened.";
    public const string NoRiskObjects = "No risk objects linked.";
    public const string EstimatedLossAmountNegative = "Estimated loss amount cannot be negative.";
    public const string ApprovedReserveRequired = "At least one approved reserve is required to move a claim to PendingPayment.";
    public const string PendingReserveBlocksWithdrawal = "Claim cannot be withdrawn while a reserve transaction is pending approval.";
    public const string SubrogationAmountZero = "Reserve amount must not be zero.";
    public const string AdjustmentAmountZero = "Adjustment amount must not be zero.";
    public const string ReverseAmountNotAllowed = "The amount of a Reverse transaction is computed by the system; omit it.";
    public const string ChangeReasonRequired = "A change reason is required.";
    public const string PendingTransactionExists = "Component has a pending transaction; retract it or wait for a decision.";
    public const string OnlyPendingCanBeDecided = "Only a transaction pending approval can be approved, rejected or retracted.";
    public const string RejectionReasonRequired = "A rejection reason is required.";
    public const string NoRejectionAuthority = "Your role does not have authority to reject this reserve amount.";
    public const string OnlySubmitterCanRetract = "Only the submitter may retract a pending reserve.";
    public const string OverrideReasonRequired = "A reason is required to change the reserve limit override.";
    public const string LastClaimantCannotBeRemoved = "The last active Claimant cannot be removed.";
    public const string PartyAlreadyRemoved = "The party has already been removed from the claim.";
    public const string OnlyWarningsCanBeAcknowledged = "Only a Warning can be acknowledged.";
    public const string OnlyOpenIssuesCanBeAcknowledged = "Only an open validation issue can be acknowledged.";
    public const string AcknowledgementNoteRequired = "An acknowledgement note is required.";
    public const string AssigneeMustBeActive = "The assigned handler must be an active user.";

    // Shape rules shared by the FluentValidation validators and the entities (ASSUMPTION wordings, D-40).
    public const string InvalidClaimSeverity = "Invalid claim severity.";
    public const string InvalidPartyRole = "Invalid party role.";
    public const string InvalidPartyType = "Invalid party type.";
    public const string PersonNameRequired = "First name and last name are required for a person.";
    public const string CompanyNameRequired = "Company name is required for a company.";
    public const string EmailInvalid = "Email address is not valid.";
    public const string InvalidAssetType = "Invalid asset type.";
    public const string AssetDescriptionRequired = "Asset description is required.";
    public const string InvalidTransactionType = "Invalid reserve transaction type.";

    public static string ValidNextStatuses(IReadOnlyCollection<ClaimStatus> statuses) =>
        statuses.Count == 0
            ? "Valid next statuses: none."
            : $"Valid next statuses: {string.Join(", ", statuses)}.";

    public static string ReasonRequired(ClaimStatus target) => $"A reason is required to move a claim to {target}.";

    public static string ClaimIsReadOnly(ClaimStatus status) => $"Claim is {status}; no changes are permitted.";

    public static string OpenReservesWarning(decimal total) =>
        string.Create(CultureInfo.InvariantCulture, $"The claim has open reserves totalling {total:0.00}.");

    public static string BalanceBelowZero(ReserveComponentType component) =>
        $"Reserve balance for {component} cannot go below zero.";

    public static string BalanceAlreadyZero(ReserveComponentType component) =>
        $"Reserve balance for {component} is already zero.";

    public static string ComponentAlreadyExists(ReserveComponentType component) =>
        $"A {component} reserve already exists on this claim; submit an Adjust transaction.";

    public static string ComponentDoesNotExist(ReserveComponentType component, ReserveTransactionType type) =>
        $"There is no {component} reserve on this claim to {type.ToString().ToLowerInvariant()}; submit an Add transaction.";

    public static string TooManyDecimalPlaces(string field) => $"{field} must have at most 4 decimal places.";
}
