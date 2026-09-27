using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.ReferenceData;

/// <summary>FRS §5.6, §9.9.</summary>
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

/// <summary>
/// Cause-of-loss reference data (FRS §5.6, §9.9), seeded by migration and scoped to the
/// organisation (D-12). Claims reference it by <see cref="Code"/> (FRS §9.2).
/// </summary>
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

    /// <summary>The "Notes" column of the FRS §5.6 seed table (not listed in §9.9; kept so the seed is exact).</summary>
    public string? Notes { get; private set; }
}
