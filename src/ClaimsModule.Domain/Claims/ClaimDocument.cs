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

    public const string FileEmptyMessage = "The file is empty.";
    public const string FileTooLargeMessage = "The file must not exceed 50 MB.";
    public const string InvalidDocumentTypeMessage = "Invalid document type.";

    private ClaimDocument()
    {
    }

    private ClaimDocument(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public DocumentType DocumentType { get; private set; }

    /// <summary>The sanitised file name, for display and as the download name (D-42: the raw client string is never stored).</summary>
    public string DocumentName { get; private set; } = null!;

    /// <summary>{organisationId}/{claimId}/{documentId}_{sanitisedFileName} (D-28).</summary>
    public string BlobPath { get; private set; } = null!;

    /// <summary>The canonical MIME type of the allowlisted format (FRS §13), never the client's declared value.</summary>
    public string ContentType { get; private set; } = null!;

    public long FileSizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public Guid? UploadedByUserId { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>
    /// Invariants: the blob path belongs to this claim and this document (a row can never point into another claim's
    /// folder), the file name has an allowlisted extension, the size is 1 byte to 50 MB.
    /// </summary>
    internal static ClaimDocument Create(
        Guid claimId,
        DocumentBlobPath blobPath,
        DocumentType documentType,
        long fileSizeBytes,
        string? notes,
        Guid uploadedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(blobPath);
        if (blobPath.ClaimId != claimId)
        {
            throw new InvalidOperationException("The blob path belongs to another claim.");
        }

        var violations = new RuleViolations();
        if (!Enum.IsDefined(documentType))
        {
            violations.Add(ErrorKeys.DocumentType, InvalidDocumentTypeMessage);
        }

        var format = DocumentFormat.FromExtension(blobPath.FileName.Extension);
        if (format is null)
        {
            violations.Add(ErrorKeys.File, DocumentFormat.NotAllowedMessage);
        }

        if (fileSizeBytes <= 0)
        {
            violations.Add(ErrorKeys.File, FileEmptyMessage);
        }
        else if (fileSizeBytes > MaxFileSizeBytes)
        {
            violations.Add(ErrorKeys.File, FileTooLargeMessage);
        }

        if (notes is { Length: > FieldLengths.Reason })
        {
            violations.Add(ErrorKeys.Notes, $"Notes must not exceed {FieldLengths.Reason} characters.");
        }

        violations.ThrowIfAny();

        return new ClaimDocument(blobPath.DocumentId)
        {
            ClaimId = claimId,
            DocumentType = documentType,
            DocumentName = blobPath.FileName.Value,
            BlobPath = blobPath.Value,
            ContentType = format!.ContentType,
            FileSizeBytes = fileSizeBytes,
            UploadedAt = now,
            UploadedByUserId = uploadedByUserId,
            Notes = Text.NullIfBlank(notes),
        };
    }
}
