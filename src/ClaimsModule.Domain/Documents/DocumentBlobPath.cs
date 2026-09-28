namespace ClaimsModule.Domain.Documents;

/// <summary>
/// Where a document's bytes live inside the <c>claim-documents</c> container (BR-D-01, FRS §13, D-28):
/// <c>{organisationId}/{claimId}/{documentId}_{sanitisedFileName}</c>. The document id prefix means two uploads with the
/// same name never overwrite each other. Every segment but the last is a GUID and the last is a
/// <see cref="SanitisedFileName"/>, so the path always has exactly three segments and cannot point outside its claim's folder.
/// </summary>
public sealed record DocumentBlobPath
{
    private DocumentBlobPath(Guid organisationId, Guid claimId, Guid documentId, SanitisedFileName fileName)
    {
        OrganisationId = organisationId;
        ClaimId = claimId;
        DocumentId = documentId;
        FileName = fileName;
        Value = $"{organisationId:D}/{claimId:D}/{documentId:D}_{fileName.Value}";
    }

    public Guid OrganisationId { get; }

    public Guid ClaimId { get; }

    public Guid DocumentId { get; }

    public SanitisedFileName FileName { get; }

    public string Value { get; }

    public static DocumentBlobPath For(Guid organisationId, Guid claimId, Guid documentId, SanitisedFileName fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (organisationId == Guid.Empty || claimId == Guid.Empty || documentId == Guid.Empty)
        {
            throw new ArgumentException("Organisation, claim and document ids are required.");
        }

        return new DocumentBlobPath(organisationId, claimId, documentId, fileName);
    }

    public override string ToString() => Value;
}
