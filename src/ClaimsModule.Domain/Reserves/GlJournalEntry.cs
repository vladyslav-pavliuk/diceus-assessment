namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// The simulated double-entry journal of one reserve change (FRS §6.5, Brief §3.5): an increase is
/// "DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {reserveAmount}".
/// A decrease reverses the two accounts and posts the absolute amount, because a journal line never
/// carries a negative amount (ASSUMPTION, D-41: the FRS gives only the increase). SubrogationRecoverable
/// uses the same accounts (NOT SPECIFIED).
/// </summary>
public sealed record GlJournalEntry(string DebitAccount, string CreditAccount, decimal Amount)
{
    public const string ChangeInOutstandingReserves = "Change in Outstanding Reserves";
    public const string OutstandingLossReserves = "Outstanding Loss Reserves";

    /// <param name="reserveDelta">The signed Amount of the ReserveHistory row (FRS §9.6).</param>
    public static GlJournalEntry ForReserveChange(decimal reserveDelta)
    {
        if (reserveDelta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reserveDelta), reserveDelta, "A reserve change is never zero (D-05).");
        }

        return reserveDelta > 0
            ? new GlJournalEntry(ChangeInOutstandingReserves, OutstandingLossReserves, reserveDelta)
            : new GlJournalEntry(OutstandingLossReserves, ChangeInOutstandingReserves, -reserveDelta);
    }

    /// <summary>For logs and the audit description, e.g. "DR Change in Outstanding Reserves / CR Outstanding Loss Reserves".</summary>
    public string Lines => $"DR {DebitAccount} / CR {CreditAccount}";
}
