using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Policies;

/// <summary>FRS §9.10.</summary>
public enum PolicyStatus
{
    Active = 1,
    Expired,
    Cancelled,
}

/// <summary>
/// A simulated policy (FRS §5.5, §9.10): reference data outside the Claim aggregate, seeded by
/// migration. A claim keeps only the id plus the denormalised number and client name.
/// </summary>
public sealed class Policy : Entity
{
    private Policy()
    {
    }

    private Policy(Guid id)
        : base(id)
    {
    }

    public string PolicyNumber { get; private set; } = null!;

    public string ClientName { get; private set; } = null!;

    public DateOnly EffectiveDate { get; private set; }

    public DateOnly ExpirationDate { get; private set; }

    public PolicyStatus Status { get; private set; }

    /// <summary>Stored as a JSON array (FRS §9.10 allows "comma-separated list or JSON array"; D-33).</summary>
    public IReadOnlyList<string> CoverageTypes { get; private set; } = [];

    public static Policy Create(
        string policyNumber,
        string clientName,
        DateOnly effectiveDate,
        DateOnly expirationDate,
        PolicyStatus status,
        IEnumerable<string> coverageTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        if (expirationDate < effectiveDate)
        {
            throw new ArgumentException("A policy cannot expire before it takes effect.", nameof(expirationDate));
        }

        return new Policy(SequentialGuid.NewGuid())
        {
            PolicyNumber = policyNumber,
            ClientName = clientName,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            Status = status,
            CoverageTypes = coverageTypes.ToArray(),
        };
    }

    /// <summary>
    /// BR-C-02: whether the loss date falls within the policy period. The loss date's UTC calendar
    /// date is compared with the policy dates, inclusive at both ends (D-32, ASSUMPTION).
    /// </summary>
    public bool CoversLossDate(DateTimeOffset lossDate)
    {
        var lossDay = DateOnly.FromDateTime(lossDate.UtcDateTime);
        return lossDay >= EffectiveDate && lossDay <= ExpirationDate;
    }
}
