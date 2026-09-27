namespace ClaimsModule.Domain.Common;

internal static class Text
{
    /// <summary>Trims the value; blank becomes null, so "no value" has one representation.</summary>
    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
