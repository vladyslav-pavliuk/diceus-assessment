namespace ClaimsModule.Domain.Documents;

/// <summary>
/// <c>{organisationId}/{claimId}/{documentId}_{sanitisedFileName}</c> (BR-D-01, D-28). The document id prefix
/// stops same-name uploads overwriting each other, and GUID segments plus a sanitised name keep the path
/// inside its claim's folder.
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
