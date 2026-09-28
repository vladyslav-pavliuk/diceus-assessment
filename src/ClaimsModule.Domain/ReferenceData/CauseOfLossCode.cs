using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.ReferenceData;

public enum PerilCategory
{
    Property = 1,
    Auto,
    Liability,
    Weather,
    Equipment,
    Crime,
    General,
}

/// <summary>Seeded, organisation-scoped reference data (D-12). Claims reference it by <see cref="Code"/>.</summary>
public sealed class CauseOfLossCode : Entity
{
    private CauseOfLossCode()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public PerilCategory PerilCategory { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>From the FRS §5.6 seed table, although §9.9 does not list it.</summary>
    public string? Notes { get; private set; }
}
