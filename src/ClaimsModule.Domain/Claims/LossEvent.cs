using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims;

/// <summary>What the reporter tells us about the loss, before the claim exists (FNOL step 1, FRS §5.2).</summary>
public sealed record LossEventDetails(
    DateTimeOffset LossDate,
    string LossDescription,
    string? LossLocation,
    string CauseOfLossCode,
    decimal? EstimatedLossAmount,
    string? PoliceReportNumber);

/// <summary>
/// The loss incident (FRS §9.2). One per claim (FRS §1.2, D-13), created with it.
/// BR-C-05 (the code exists and is active) needs reference data, so it is checked by the validator
/// and backed by a foreign key; this entity guards the rules it can see on its own.
/// </summary>
public sealed class LossEvent : Entity
{
    public const int MinimumDescriptionLength = 20;

    private LossEvent()
    {
    }

    private LossEvent(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    /// <summary>Stored in UTC (FRS §14.2, D-32).</summary>
    public DateTimeOffset LossDate { get; private set; }

    public string LossDescription { get; private set; } = null!;

    public string? LossLocation { get; private set; }

    public string CauseOfLossCode { get; private set; } = null!;

    public decimal? EstimatedLossAmount { get; private set; }

    /// <summary>When the claim was formally reported: the server time of the FNOL (D-32).</summary>
    public DateTimeOffset ReportDate { get; private set; }

    public string? PoliceReportNumber { get; private set; }

    internal static LossEvent Create(Guid claimId, LossEventDetails details, DateTimeOffset now)
    {
        var violations = new RuleViolations();

        if (details.LossDate == default)
        {
            violations.Add(ErrorKeys.LossDate, DomainMessages.LossDateRequired);
        }
        else if (details.LossDate > now)
        {
            violations.Add(ErrorKeys.LossDate, DomainMessages.LossDateInFuture); // BR-C-01
        }

        var description = details.LossDescription?.Trim() ?? string.Empty;
        if (description.Length < MinimumDescriptionLength)
        {
            violations.Add(ErrorKeys.LossDescription, DomainMessages.LossDescriptionTooShort); // BR-C-07
        }

        var causeOfLossCode = details.CauseOfLossCode?.Trim() ?? string.Empty;
        if (causeOfLossCode.Length == 0)
        {
            violations.Add(ErrorKeys.CauseOfLossCode, DomainMessages.CauseOfLossCodeInvalid); // BR-C-05
        }

        if (details.EstimatedLossAmount is { } estimate)
        {
            if (estimate < 0)
            {
                violations.Add(ErrorKeys.EstimatedLossAmount, DomainMessages.EstimatedLossAmountNegative);
            }
            else if (!Amounts.HasValidScale(estimate))
            {
                violations.Add(ErrorKeys.EstimatedLossAmount, DomainMessages.TooManyDecimalPlaces("Estimated loss amount"));
            }
        }

        violations.ThrowIfAny();

        return new LossEvent(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            LossDate = details.LossDate.ToUniversalTime(),
            LossDescription = description,
            LossLocation = Text.NullIfBlank(details.LossLocation),
            CauseOfLossCode = causeOfLossCode,
            EstimatedLossAmount = details.EstimatedLossAmount,
            ReportDate = now,
            PoliceReportNumber = Text.NullIfBlank(details.PoliceReportNumber),
        };
    }
}
