namespace ClaimsModule.Domain.Common;

/// <summary>Rules shared by every monetary amount (FRS §15.1: DECIMAL(19,4)).</summary>
public static class Amounts
{
    public const int Scale = 4;

    /// <summary>
    /// The largest magnitude DECIMAL(19,4) can hold (15 integer digits). Larger values would fail
    /// on insert instead of being rejected with a message.
    /// </summary>
    public const decimal MaxMagnitude = 999_999_999_999_999.9999m;

    /// <summary>
    /// False when the value has more than 4 decimal places or does not fit DECIMAL(19,4); SQL Server
    /// would otherwise round or reject the value silently.
    /// </summary>
    public static bool HasValidScale(decimal amount) =>
        Math.Abs(amount) <= MaxMagnitude && decimal.Round(amount, Scale) == amount;
}
