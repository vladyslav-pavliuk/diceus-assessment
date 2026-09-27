using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Tests.Claims;

public sealed class ClaimNumberTests
{
    [Theory]
    [InlineData(2026, 142, "CLM-2026-0000142")]
    [InlineData(2026, 1, "CLM-2026-0000001")]
    [InlineData(2031, 9_999_999, "CLM-2031-9999999")]
    public void BR_C_04_Claim_number_format_is_CLM_year_7_digits(int year, int sequence, string expected)
    {
        var claimNumber = ClaimNumber.Create(year, sequence);

        claimNumber.Value.ShouldBe(expected);
        ClaimNumber.TryParse(expected, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(claimNumber);
    }

    [Fact]
    public void BR_C_04_Sequence_above_seven_digits_is_rejected()
    {
        Should.Throw<BusinessRuleViolationException>(() => ClaimNumber.Create(2026, ClaimNumber.MaxSequence + 1))
            .Errors[ErrorKeys.ClaimNumber].ShouldBe(["The claim number sequence for 2026 is exhausted."]);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(999, 1)]
    [InlineData(10_000, 1)]
    public void BR_C_04_Year_and_sequence_must_fit_the_format(int year, int sequence)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ClaimNumber.Create(year, sequence));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CLM-2026-142")]
    [InlineData("clm-2026-0000142")]
    [InlineData("CLM-2026-0000000")]
    [InlineData("CLM-2026-00001420")]
    [InlineData(" CLM-2026-0000142")]
    public void BR_C_04_Malformed_claim_numbers_do_not_parse(string? value)
    {
        ClaimNumber.TryParse(value, out var parsed).ShouldBeFalse();
        parsed.ShouldBeNull();
    }
}
