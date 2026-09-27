using System.Text;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Documents;

/// <summary>
/// A file name that is safe to use as the last segment of a blob path (BR-D-01, D-28):
/// directory components are stripped (so "../../x" cannot escape the claim's folder), control and
/// reserved characters are removed or replaced, Unicode is normalised to NFC, Windows device names
/// are neutralised, and the length is capped at 200 characters with the extension kept.
/// </summary>
public sealed record SanitisedFileName
{
    public const int MaxLength = 200;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private SanitisedFileName(string value) => Value = value;

    public string Value { get; }

    public static SanitisedFileName From(string? originalFileName)
    {
        var name = (originalFileName ?? string.Empty).Normalize(NormalizationForm.FormC);

        // Keep only the last path segment, whichever separator the client used.
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            builder.Append(character is '<' or '>' or ':' or '"' or '|' or '?' or '*' ? '_' : character);
        }

        // Windows ignores trailing dots and spaces, so "a.pdf." and "a.pdf" would collide.
        name = builder.ToString().Trim().TrimEnd('.', ' ');

        if (name.Length == 0 || name.All(character => character == '.'))
        {
            throw new BusinessRuleViolationException(ErrorKeys.FileName, "The file name is empty or invalid.");
        }

        var dot = name.LastIndexOf('.');
        var stem = dot > 0 ? name[..dot] : name;
        if (ReservedDeviceNames.Contains(stem))
        {
            name = "_" + name;
        }

        return new SanitisedFileName(Truncate(name));
    }

    public override string ToString() => Value;

    private static string Truncate(string name)
    {
        if (name.Length <= MaxLength)
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        var extension = dot > 0 && name.Length - dot <= 16 ? name[dot..] : string.Empty;
        var cut = MaxLength - extension.Length;
        if (char.IsHighSurrogate(name[cut - 1]))
        {
            cut--; // never split a surrogate pair
        }

        return name[..cut] + extension;
    }
}
