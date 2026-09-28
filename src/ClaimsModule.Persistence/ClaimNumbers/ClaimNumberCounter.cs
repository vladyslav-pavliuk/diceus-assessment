namespace ClaimsModule.Persistence.ClaimNumbers;

/// <summary>A persistence detail with no domain behaviour, so it lives here (D-10, D-14).</summary>
internal sealed class ClaimNumberCounter
{
    public Guid OrganisationId { get; private set; }

    public int Year { get; private set; }

    public int LastValue { get; private set; }
}
