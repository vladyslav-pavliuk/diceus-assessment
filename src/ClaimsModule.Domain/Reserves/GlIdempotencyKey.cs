using System.Globalization;

namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// BR-R-06 / FRS §6.5: Reserve:{ReserveId}:Change:{ChangeSequence}, where ReserveId is the reserve
/// component id and ChangeSequence is the per-component sequence of the transaction (D-23).
/// </summary>
public static class GlIdempotencyKey
{
    public const int MaxLength = 200;

    public static string For(Guid reserveComponentId, int changeSequence) =>
        string.Create(CultureInfo.InvariantCulture, $"Reserve:{reserveComponentId:D}:Change:{changeSequence}");
}
