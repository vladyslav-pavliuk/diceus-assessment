using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Documents;

/// <summary>BR-D-01, DOC-05/06/07/08: document metadata recorded through the Claim aggregate (FRS §9.7, §13, D-28, D-42).</summary>
public sealed class ClaimDocumentTests
{
    private static readonly Guid OrganisationId = Guid.NewGuid();

    [Fact]
    public void BR_D_01_Blob_path_is_org_claim_and_document_id_prefixed_name()
    {
        var claimId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var path = DocumentBlobPath.For(OrganisationId, claimId, documentId, SanitisedFileName.From("../../other-org/police report.pdf"));

        path.Value.ShouldBe($"{OrganisationId}/{claimId}/{documentId}_police report.pdf");
        path.Value.Split('/').Length.ShouldBe(3);
    }

    [Fact]
    public void DOC_08_Adding_a_document_records_the_metadata_and_raises_DocumentUploaded()
    {
        var claim = DraftClaim();
        var path = PathFor(claim, "Police Report.PDF");

        var document = claim.AddDocument(path, DocumentType.PoliceReport, 2_048, "  From the scene.  ", Handler, Now);

        document.Id.ShouldBe(path.DocumentId);
        document.ClaimId.ShouldBe(claim.Id);
        document.DocumentName.ShouldBe("Police Report.PDF");
        document.BlobPath.ShouldBe(path.Value);
        document.ContentType.ShouldBe("application/pdf"); // canonical, from the allowlist
        document.FileSizeBytes.ShouldBe(2_048);
        document.UploadedAt.ShouldBe(Now);
        document.UploadedByUserId.ShouldBe(Handler.UserId);
        document.Notes.ShouldBe("From the scene.");
        claim.Documents.ShouldBe([document]);

        claim.DomainEvents.ShouldHaveSingleItem().ShouldBe(new DocumentUploaded(
            claim.Id, document.Id, "Police Report.PDF", DocumentType.PoliceReport, "application/pdf", 2_048)); // DOC-07
    }

    [Theory]
    [InlineData(0L, ClaimDocument.FileEmptyMessage)]
    [InlineData(ClaimDocument.MaxFileSizeBytes + 1, ClaimDocument.FileTooLargeMessage)]
    public void DOC_06_Size_must_be_between_1_byte_and_50_MB(long size, string message)
    {
        var claim = DraftClaim();

        Should.Throw<BusinessRuleViolationException>(() => claim.AddDocument(PathFor(claim), DocumentType.Other, size, null, Handler, Now))
            .Errors["File"].ShouldBe([message]);
        claim.AddDocument(PathFor(claim), DocumentType.Other, ClaimDocument.MaxFileSizeBytes, null, Handler, Now).FileSizeBytes
            .ShouldBe(ClaimDocument.MaxFileSizeBytes); // exactly 50 MB is allowed
    }

    [Fact]
    public void DOC_05_The_aggregate_refuses_an_extension_outside_the_allowlist()
    {
        var claim = DraftClaim();

        Should.Throw<BusinessRuleViolationException>(() => claim.AddDocument(PathFor(claim, "run.exe"), DocumentType.Other, 10, null, Handler, Now))
            .Errors["File"].ShouldBe([DocumentFormat.NotAllowedMessage]);
    }

    [Fact]
    public void BR_D_01_A_document_cannot_point_into_another_claims_folder()
    {
        var claim = DraftClaim();
        var foreignPath = DocumentBlobPath.For(OrganisationId, Guid.NewGuid(), Guid.NewGuid(), SanitisedFileName.From("a.pdf"));

        Should.Throw<InvalidOperationException>(() => claim.AddDocument(foreignPath, DocumentType.Other, 10, null, Handler, Now));
        claim.Documents.ShouldBeEmpty();
    }

    [Fact]
    public void DOC_08_Undefined_document_type_is_rejected()
    {
        var claim = DraftClaim();

        Should.Throw<BusinessRuleViolationException>(() => claim.AddDocument(PathFor(claim), (DocumentType)0, 10, null, Handler, Now))
            .Errors["DocumentType"].ShouldBe([ClaimDocument.InvalidDocumentTypeMessage]);
    }

    private static DocumentBlobPath PathFor(Claim claim, string fileName = "report.pdf") =>
        DocumentBlobPath.For(OrganisationId, claim.Id, SequentialGuid.NewGuid(), SanitisedFileName.From(fileName));
}
