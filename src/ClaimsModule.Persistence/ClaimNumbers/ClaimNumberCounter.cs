namespace ClaimsModule.Persistence.ClaimNumbers;

/// <summary>
/// The last claim-number sequence handed out per organisation and year (D-10). A persistence detail
/// with no domain behaviour, so it lives here and not in the Domain. It is not soft-deletable and has
/// no audit columns: it is a counter, not a business record (D-14).
/// </summary>
internal sealed class ClaimNumberCounter
{
    public Guid OrganisationId { get; private set; }

    public int Year { get; private set; }

    public int LastValue { get; private set; }
}
