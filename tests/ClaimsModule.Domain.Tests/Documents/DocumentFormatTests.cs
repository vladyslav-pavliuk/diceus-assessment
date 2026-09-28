using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Domain.Tests.Documents;

/// <summary>DOC-05: the FRS §13 MIME allowlist, claimed by the extension and checked against the declared Content-Type (D-42).</summary>
public sealed class DocumentFormatTests
{
    [Theory]
    [InlineData("police-report.pdf", "application/pdf", "application/pdf")]
    [InlineData("photo.JPG", "image/jpeg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpg", "image/jpeg")]
    [InlineData("damage.png", "image/png", "image/png")]
    [InlineData("statement.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("costs.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("notes.txt", "text/plain; charset=utf-8", "text/plain")]
    [InlineData("ledger.csv", "text/csv", "text/csv")]
    [InlineData("ledger.csv", "application/vnd.ms-excel", "text/csv")] // what Windows browsers declare for .csv
    [InlineData("scan.pdf", "application/octet-stream", "application/pdf")] // the browser did not know
    [InlineData("scan.pdf", "", "application/pdf")]
    [InlineData("scan.pdf", null, "application/pdf")]
    public void DOC_05_Allowlisted_extension_with_a_consistent_declared_type_resolves_to_the_canonical_type(
        string fileName, string? declared, string expectedContentType)
    {
        DocumentFormat.Resolve(SanitisedFileName.From(fileName), declared).ContentType.ShouldBe(expectedContentType);
    }

    [Theory]
    [InlineData("setup.exe", "application/x-msdownload")]
    [InlineData("page.html", "text/html")]
    [InlineData("image.svg", "image/svg+xml")] // SVG can carry script
    [InlineData("macro.docm", "application/vnd.ms-word.document.macroEnabled.12")]
    [InlineData("old.doc", "application/msword")]
    [InlineData("invoice.pdf.exe", "application/pdf")] // the last extension decides
    [InlineData("README", "text/plain")]
    public void DOC_05_Extension_outside_the_allowlist_is_rejected(string fileName, string declared)
    {
        Should.Throw<BusinessRuleViolationException>(() => DocumentFormat.Resolve(SanitisedFileName.From(fileName), declared))
            .Errors["File"].ShouldBe([DocumentFormat.NotAllowedMessage]);
    }

    [Theory]
    [InlineData("notes.txt", "text/html")]
    [InlineData("scan.pdf", "image/png")]
    [InlineData("photo.png", "application/x-msdownload")]
    public void DOC_05_Declared_type_contradicting_the_extension_is_rejected(string fileName, string declared)
    {
        Should.Throw<BusinessRuleViolationException>(() => DocumentFormat.Resolve(SanitisedFileName.From(fileName), declared))
            .Errors["File"].ShouldBe([DocumentFormat.DeclaredTypeMismatchMessage]);
    }

    [Fact]
    public void DOC_05_The_allowlist_is_exactly_the_seven_FRS_types()
    {
        DocumentFormat.All.Select(format => format.Name).ShouldBe(["PDF", "JPEG", "PNG", "DOCX", "XLSX", "TXT", "CSV"]);
    }

    [Fact]
    public void DOC_02_Only_PDF_and_images_display_inline()
    {
        DocumentFormat.All.Where(format => format.DisplayInline).ShouldBe([DocumentFormat.Pdf, DocumentFormat.Jpeg, DocumentFormat.Png]);
    }

    [Fact]
    public void DOC_05_Stored_content_type_maps_back_to_its_format()
    {
        DocumentFormat.FromContentType("image/png").ShouldBe(DocumentFormat.Png);
        DocumentFormat.FromContentType("text/html").ShouldBeNull();
    }
}
