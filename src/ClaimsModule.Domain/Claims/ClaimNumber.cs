using System.Globalization;
using System.Text.RegularExpressions;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Claims;

/// <summary>
/// CLM-{YYYY}-{7-digit zero-padded sequence}, e.g. CLM-2026-0000142 (FRS §5.3, BR-C-04).
/// The sequence comes from the per-organisation, per-year counter table inside the claim-creation
/// transaction (D-10); this type only owns the format.
/// </summary>
public sealed partial record ClaimNumber
{
    public const int MaxSequence = 9_999_999;

    private ClaimNumber(int year, int sequence)
    {
        Year = year;
        Sequence = sequence;
        Value = string.Create(CultureInfo.InvariantCulture, $"CLM-{year:D4}-{sequence:D7}");
    }

    public int Year { get; }

    public int Sequence { get; }

    public string Value { get; }

    public static ClaimNumber Create(int year, int sequence)
    {
        if (year is < 1000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "The year must have four digits.");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "The sequence starts at 1.");
        }

        if (sequence > MaxSequence)
        {
            // D-10: unreachable in practice, but the format has room for 7 digits only.
            throw new BusinessRuleViolationException(
                ErrorKeys.ClaimNumber,
                $"The claim number sequence for {year} is exhausted.");
        }

        return new ClaimNumber(year, sequence);
    }

    public static bool TryParse(string? value, out ClaimNumber? claimNumber)
    {
        claimNumber = null;
        var match = value is null ? Match.Empty : Format().Match(value);
        if (!match.Success)
        {
            return false;
        }

        var sequence = int.Parse(match.Groups["sequence"].Value, CultureInfo.InvariantCulture);
        if (sequence < 1)
        {
            return false;
        }

        claimNumber = new ClaimNumber(int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture), sequence);
        return true;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^CLM-(?<year>[1-9]\d{3})-(?<sequence>\d{7})$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
