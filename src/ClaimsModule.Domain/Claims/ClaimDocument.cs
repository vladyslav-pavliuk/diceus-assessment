using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Domain.Claims;

/// <summary>
/// Metadata of a document stored in blob storage (FRS §9.7, §13). The bytes never enter the
/// database. Added through the Claim, so the read-only rule for Closed/Withdrawn claims applies (D-26).
/// </summary>
public sealed class ClaimDocument : Entity
{
    /// <summary>FRS §13: "implement a reasonable 50 MB limit".</summary>
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;

    private ClaimDocument()
    {
    }

    private ClaimDocument(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public DocumentType DocumentType { get; private set; }

    /// <summary>The file name as uploaded, for display.</summary>
    public string DocumentName { get; private set; } = null!;

    /// <summary>{organisationId}/{claimId}/{documentId}_{sanitisedFileName} (D-28).</summary>
    public string BlobPath { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long FileSizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public Guid? UploadedByUserId { get; private set; }

    public string? Notes { get; private set; }

    internal static ClaimDocument Create(
        Guid documentId,
        Guid claimId,
        DocumentType documentType,
        string documentName,
        string blobPath,
        string contentType,
        long fileSizeBytes,
        string? notes,
        Guid uploadedByUserId,
        DateTimeOffset now)
    {
        var violations = new RuleViolations();
        if (!Enum.IsDefined(documentType))
        {
            violations.Add(ErrorKeys.Document, "Invalid document type.");
        }

        if (fileSizeBytes is <= 0 or > MaxFileSizeBytes)
        {
            violations.Add(ErrorKeys.Document, "The file must be between 1 byte and 50 MB.");
        }

        if (string.IsNullOrWhiteSpace(documentName) || string.IsNullOrWhiteSpace(blobPath) || string.IsNullOrWhiteSpace(contentType))
        {
            violations.Add(ErrorKeys.Document, "Document name, storage path and content type are required.");
        }

        violations.ThrowIfAny();

        return new ClaimDocument(documentId)
        {
            ClaimId = claimId,
            DocumentType = documentType,
            DocumentName = documentName.Trim(),
            BlobPath = blobPath,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            UploadedAt = now,
            UploadedByUserId = uploadedByUserId,
            Notes = Text.NullIfBlank(notes),
        };
    }
}
