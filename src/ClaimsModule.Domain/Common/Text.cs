namespace ClaimsModule.Domain.Common;

internal static class Text
{
    /// <summary>Trims the value and turns blank into null.</summary>
    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
