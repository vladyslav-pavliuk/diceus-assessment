using ClaimsModule.Application.Claims.Commands.UploadClaimDocument;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Tests.Documents;

/// <summary>Request shape of POST /api/claims/{id}/documents (DOC-06, D-42). Errors are keyed by the form field ("File", "Notes").</summary>
public sealed class UploadClaimDocumentCommandValidatorTests
{
    private static readonly UploadClaimDocumentCommand Valid =
        new(Guid.NewGuid(), new SizedStream(1_024), "report.pdf", "application/pdf", DocumentType.PoliceReport, "From the scene.");

    [Fact]
    public void API_08_A_complete_upload_is_valid()
    {
        Validate(Valid).IsValid.ShouldBeTrue();
        Validate(Valid with { DocumentType = null, Notes = null }).IsValid.ShouldBeTrue(); // type defaults to Other
    }

    [Fact]
    public void API_08_A_file_is_required()
    {
        Messages(Valid with { Content = null, FileName = null }, "File").ShouldBe([UploadClaimDocumentCommandValidator.FileRequiredMessage]);
    }

    [Fact]
    public void DOC_06_Exactly_50_MB_is_accepted_one_byte_more_is_not()
    {
        Validate(Valid with { Content = new SizedStream(ClaimDocument.MaxFileSizeBytes) }).IsValid.ShouldBeTrue();
        Messages(Valid with { Content = new SizedStream(ClaimDocument.MaxFileSizeBytes + 1) }, "File").ShouldBe([ClaimDocument.FileTooLargeMessage]);
    }

    [Fact]
    public void DOC_06_An_empty_file_is_rejected()
    {
        Messages(Valid with { Content = new SizedStream(0) }, "File").ShouldBe([ClaimDocument.FileEmptyMessage]);
    }

    [Fact]
    public void DOC_08_Field_lengths_and_document_type()
    {
        Messages(Valid with { FileName = new string('a', 252) + ".pdf" }, "File").ShouldBe(["File name must not exceed 255 characters."]);
        Messages(Valid with { Notes = new string('n', 501) }, "Notes").ShouldBe(["Notes must not exceed 500 characters."]);
        Messages(Valid with { DocumentType = (DocumentType)42 }, "DocumentType").ShouldBe([ClaimDocument.InvalidDocumentTypeMessage]);
    }

    private static FluentValidation.Results.ValidationResult Validate(UploadClaimDocumentCommand command) =>
        new UploadClaimDocumentCommandValidator().Validate(command);

    private static IEnumerable<string> Messages(UploadClaimDocumentCommand command, string key) =>
        Validate(command).Errors.Where(error => error.PropertyName == key).Select(error => error.ErrorMessage);

    /// <summary>A seekable stream of a given length that holds no data, so a 50 MB boundary costs no memory.</summary>
    private sealed class SizedStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => Position = offset;

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
