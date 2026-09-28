using System.Globalization;
using System.Text;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Documents;

/// <summary>
/// Safe as the last segment of a blob path (BR-D-01, D-42). NFKC normalisation runs <em>first</em>, so look-alikes
/// such as fullwidth "．．／" become "../" before directory components are stripped. Invisible characters are
/// removed, reserved characters and the slash look-alikes NFKC keeps become "_", Windows device names are
/// neutralised, and the length is capped with the extension kept.
/// </summary>
public sealed record SanitisedFileName
{
    public const int MaxLength = 200;

    public const string InvalidMessage = "The file name is empty or invalid.";

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private SanitisedFileName(string value) => Value = value;

    public string Value { get; }

    /// <summary>Including the dot; empty when there is none.</summary>
    public string Extension
    {
        get
        {
            var dot = Value.LastIndexOf('.');
            return dot > 0 ? Value[dot..] : string.Empty;
        }
    }

    public static SanitisedFileName From(string? originalFileName)
    {
        var name = DropUnpairedSurrogates(originalFileName ?? string.Empty).Normalize(NormalizationForm.FormKC);

        // Keep only the last path segment, whichever separator the client used.
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (IsInvisible(character))
            {
                continue;
            }

            builder.Append(IsReservedOrSlashLookalike(character) ? '_' : character);
        }

        // Windows ignores trailing dots and spaces, so "a.pdf." and "a.pdf" would collide.
        name = builder.ToString().Trim().TrimEnd('.', ' ');

        if (name.Length == 0 || name.All(character => character == '.'))
        {
            throw new BusinessRuleViolationException(ErrorKeys.File, InvalidMessage);
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

    private static string DropUnpairedSurrogates(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsHighSurrogate(character) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                builder.Append(character).Append(value[++index]);
            }
            else if (!char.IsSurrogate(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool IsInvisible(char character) =>
        char.GetUnicodeCategory(character) is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator;

    private static bool IsReservedOrSlashLookalike(char character) =>
        character is '<' or '>' or ':' or '"' or '|' or '?' or '*'
            or '\u2215' // DIVISION SLASH
            or '\u2044' // FRACTION SLASH
            or '\u29F8' // BIG SOLIDUS
            or '\u29F9' // BIG REVERSE SOLIDUS
            or '\u2216'; // SET MINUS

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
