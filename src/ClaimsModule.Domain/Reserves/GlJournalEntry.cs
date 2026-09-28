namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// Simulated double-entry journal of one reserve change (FRS §6.5). A decrease swaps the accounts and
/// posts the absolute amount, since a journal line is never negative (D-41).
/// </summary>
public sealed record GlJournalEntry(string DebitAccount, string CreditAccount, decimal Amount)
{
    public const string ChangeInOutstandingReserves = "Change in Outstanding Reserves";
    public const string OutstandingLossReserves = "Outstanding Loss Reserves";

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

    public string Lines => $"DR {DebitAccount} / CR {CreditAccount}";
}
