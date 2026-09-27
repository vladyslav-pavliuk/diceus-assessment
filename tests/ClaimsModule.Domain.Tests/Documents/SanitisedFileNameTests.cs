using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Domain.Tests.Documents;

/// <summary>BR-D-01: the file name must be sanitised to remove path traversal characters (D-28).</summary>
public sealed class SanitisedFileNameTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system.ini", "system.ini")]
    [InlineData("/var/uploads/report.pdf", "report.pdf")]
    [InlineData("C:\\Users\\alex\\invoice.pdf", "invoice.pdf")]
    [InlineData("claims/../../other-org/secret.pdf", "secret.pdf")]
    public void BR_D_01_Path_traversal_is_stripped(string original, string expected)
    {
        SanitisedFileName.From(original).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("in\u0000voice\u001f.pdf", "invoice.pdf")]
    [InlineData("what?<now>|\"*:.pdf", "what__now_____.pdf")]
    [InlineData("  report.pdf. . ", "report.pdf")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("lpt1", "_lpt1")]
    public void BR_D_01_Control_reserved_and_device_names_are_neutralised(string original, string expected)
    {
        SanitisedFileName.From(original).Value.ShouldBe(expected);
    }

    [Fact]
    public void BR_D_01_Unicode_is_normalised_to_NFC()
    {
        SanitisedFileName.From("Re\u0301sume\u0301.pdf").Value.ShouldBe("R\u00e9sum\u00e9.pdf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../")]
    [InlineData("folder/")]
    [InlineData("\u0001\u0002")]
    public void BR_D_01_Names_that_are_empty_after_sanitising_are_rejected(string? original)
    {
        Should.Throw<BusinessRuleViolationException>(() => SanitisedFileName.From(original));
    }

    [Fact]
    public void BR_D_01_Long_names_are_capped_and_keep_their_extension()
    {
        var sanitised = SanitisedFileName.From(new string('a', 300) + ".pdf").Value;

        sanitised.Length.ShouldBe(SanitisedFileName.MaxLength);
        sanitised.ShouldEndWith(".pdf");
    }
}
