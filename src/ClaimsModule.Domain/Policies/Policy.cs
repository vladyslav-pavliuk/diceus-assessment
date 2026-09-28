using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Policies;

public enum PolicyStatus
{
    Active = 1,
    Expired,
    Cancelled,
}

/// <summary>Simulated, seeded policy data, outside the Claim aggregate (FRS §5.5).</summary>
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

    /// <summary>Stored as a JSON array (D-33).</summary>
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

    /// <summary>BR-C-02: compares the loss date's UTC calendar date, inclusive at both ends (D-32).</summary>
    public bool CoversLossDate(DateTimeOffset lossDate)
    {
        var lossDay = DateOnly.FromDateTime(lossDate.UtcDateTime);
        return lossDay >= EffectiveDate && lossDay <= ExpirationDate;
    }
}
