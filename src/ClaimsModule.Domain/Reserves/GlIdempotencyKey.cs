using System.Globalization;

namespace ClaimsModule.Domain.Reserves;

/// <summary>BR-R-06: Reserve:{reserveComponentId}:Change:{changeSequence} (D-23).</summary>
public static class GlIdempotencyKey
{
    public const int MaxLength = 200;

    public static string For(Guid reserveComponentId, int changeSequence) =>
        string.Create(CultureInfo.InvariantCulture, $"Reserve:{reserveComponentId:D}:Change:{changeSequence}");
}
