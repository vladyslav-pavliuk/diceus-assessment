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

    [Theory]
    [InlineData("\\\\fileserver\\share\\claims\\report.pdf", "report.pdf")] // UNC path
    [InlineData("C:secret.pdf", "C_secret.pdf")] // drive-relative: no separator, the colon is neutralised
    [InlineData("report.pdf:hidden.exe", "report.pdf_hidden.exe")] // NTFS alternate data stream
    [InlineData("%2e%2e%2fsecret.pdf", "%2e%2e%2fsecret.pdf")] // URL-encoded traversal is only text in one segment
    [InlineData("..pdf", "..pdf")] // leading dots stay inside the segment
    public void BR_D_01_Absolute_drive_UNC_and_stream_names_stay_one_segment(string original, string expected)
    {
        SanitisedFileName.From(original).Value.ShouldBe(expected);
    }

    [Fact]
    public void BR_D_01_Unicode_is_normalised_to_NFKC()
    {
        SanitisedFileName.From("Re\u0301sume\u0301.pdf").Value.ShouldBe("R\u00e9sum\u00e9.pdf"); // composed, as NFC
        SanitisedFileName.From("\uFB01le.pdf").Value.ShouldBe("file.pdf"); // ligature "ﬁ" folded
    }

    /// <summary>Compatibility look-alikes are folded by NFKC before the separators are looked for, so they cannot hide a traversal.</summary>
    [Theory]
    [InlineData("\uFF0E\uFF0E\uFF0F\uFF0E\uFF0E\uFF0Fsecret.pdf", "secret.pdf")] // fullwidth "．．／．．／"
    [InlineData("\uFF0E\uFF0E\uFF3Cwin.ini.txt", "win.ini.txt")] // fullwidth reverse solidus
    [InlineData("\u2025/\u2025/secret.pdf", "secret.pdf")] // TWO DOT LEADER "‥" is ".."
    [InlineData("\uFE68\uFE68x\uFE68report.pdf", "report.pdf")] // SMALL REVERSE SOLIDUS
    public void BR_D_01_Fullwidth_and_compatibility_traversal_is_stripped(string original, string expected)
    {
        SanitisedFileName.From(original).Value.ShouldBe(expected);
    }

    /// <summary>Slash look-alikes that NFKC keeps are not separators to any file system, but they read as one; they become "_".</summary>
    [Theory]
    [InlineData("..\u2215..\u2215secret.pdf", ".._.._secret.pdf")] // DIVISION SLASH
    [InlineData("..\u2044secret.pdf", ".._secret.pdf")] // FRACTION SLASH
    [InlineData("..\u29F8secret.pdf", ".._secret.pdf")] // BIG SOLIDUS
    [InlineData("..\u2216secret.pdf", ".._secret.pdf")] // SET MINUS
    public void BR_D_01_Slash_lookalikes_are_neutralised(string original, string expected)
    {
        var sanitised = SanitisedFileName.From(original).Value;

        sanitised.ShouldBe(expected);
        sanitised.IndexOfAny(['/', '\\']).ShouldBe(-1);
    }

    /// <summary>
    /// Invisible characters are removed: a right-to-left override would make "invoice\u202Efdp.exe" display as
    /// "invoiceexe.pdf"; zero-width and BOM characters would make two different names look identical.
    /// </summary>
    [Theory]
    [InlineData("invoice\u202Efdp.exe", "invoicefdp.exe")] // RIGHT-TO-LEFT OVERRIDE
    [InlineData("in\u200Bvoi\u200Dce.pdf", "invoice.pdf")] // ZERO WIDTH SPACE / JOINER
    [InlineData("\uFEFFreport.pdf", "report.pdf")] // BYTE ORDER MARK
    [InlineData("re\u2066port\u2069.pdf", "report.pdf")] // LEFT-TO-RIGHT ISOLATE / POP
    [InlineData("line\u2028break.pdf", "linebreak.pdf")] // LINE SEPARATOR
    [InlineData("soft\u00ADhyphen.pdf", "softhyphen.pdf")] // SOFT HYPHEN
    public void BR_D_01_Invisible_and_bidi_characters_are_removed(string original, string expected)
    {
        SanitisedFileName.From(original).Value.ShouldBe(expected);
    }

    /// <summary>An unpaired surrogate makes string.Normalize throw; it must be a clean result, never a 500.</summary>
    /// <remarks>A [Fact], not [InlineData]: xUnit serialises theory data, which turns a lone surrogate into U+FFFD.</remarks>
    [Fact]
    public void BR_D_01_Unpaired_surrogates_are_dropped()
    {
        SanitisedFileName.From("a\uD800b\uDC00c.pdf").Value.ShouldBe("abc.pdf");
        SanitisedFileName.From("\U0001F4C4 scan.pdf").Value.ShouldBe("\U0001F4C4 scan.pdf"); // a real pair is kept
        Should.Throw<BusinessRuleViolationException>(() => SanitisedFileName.From("\uD800"))
            .Errors["File"].ShouldBe([SanitisedFileName.InvalidMessage]);
    }

    [Fact]
    public void BR_D_01_Extension_is_the_last_dot_segment()
    {
        SanitisedFileName.From("scan.2026.PDF").Extension.ShouldBe(".PDF");
        SanitisedFileName.From("README").Extension.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../")]
    [InlineData("folder/")]
    [InlineData("\u0001\u0002")]
    [InlineData("\uFF0E\uFF0E")] // fullwidth ".."
    [InlineData("\u202E\u200B")] // only invisible characters
    public void BR_D_01_Names_that_are_empty_after_sanitising_are_rejected(string? original)
    {
        Should.Throw<BusinessRuleViolationException>(() => SanitisedFileName.From(original))
            .Errors["File"].ShouldBe([SanitisedFileName.InvalidMessage]);
    }

    [Fact]
    public void BR_D_01_Long_names_are_capped_and_keep_their_extension()
    {
        var sanitised = SanitisedFileName.From(new string('a', 300) + ".pdf").Value;

        sanitised.Length.ShouldBe(SanitisedFileName.MaxLength);
        sanitised.ShouldEndWith(".pdf");
    }
}
