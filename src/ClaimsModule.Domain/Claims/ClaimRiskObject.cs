using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims;

/// <summary>A damaged asset as entered at intake or through "Add Risk Object" (FRS §5.2 step 2, §9.4).</summary>
public sealed record RiskObjectDetails(
    AssetType AssetType,
    string AssetDescription,
    string? DamageDescription,
    string? AssetReference,
    bool IsPrimary = false);

/// <summary>An asset affected by the loss (FRS §9.4). At most one per claim is primary.</summary>
public sealed class ClaimRiskObject : Entity
{
    private ClaimRiskObject()
    {
    }

    private ClaimRiskObject(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public AssetType AssetType { get; private set; }

    public string AssetDescription { get; private set; } = null!;

    public string? DamageDescription { get; private set; }

    public bool IsPrimary { get; private set; }

    public string? AssetReference { get; private set; }

    internal static ClaimRiskObject Create(Guid claimId, RiskObjectDetails details, bool isPrimary)
    {
        var violations = new RuleViolations();
        if (!Enum.IsDefined(details.AssetType))
        {
            violations.Add(ErrorKeys.RiskObjects, DomainMessages.InvalidAssetType);
        }

        var description = Text.NullIfBlank(details.AssetDescription);
        if (description is null)
        {
            violations.Add(ErrorKeys.RiskObjects, DomainMessages.AssetDescriptionRequired);
        }

        violations.ThrowIfAny();

        return new ClaimRiskObject(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            AssetType = details.AssetType,
            AssetDescription = description!,
            DamageDescription = Text.NullIfBlank(details.DamageDescription),
            AssetReference = Text.NullIfBlank(details.AssetReference),
            IsPrimary = isPrimary,
        };
    }

    internal void ClearPrimary() => IsPrimary = false;
}
