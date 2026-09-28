namespace ClaimsModule.Domain.Common;

/// <summary>Monetary amounts are DECIMAL(19,4) (FRS §15.1).</summary>
public static class Amounts
{
    public const int Scale = 4;

    public const decimal MaxMagnitude = 999_999_999_999_999.9999m;

    /// <summary>Rejects values SQL Server would otherwise round silently or fail to insert.</summary>
    public static bool HasValidScale(decimal amount) =>
        Math.Abs(amount) <= MaxMagnitude && decimal.Round(amount, Scale) == amount;
}
