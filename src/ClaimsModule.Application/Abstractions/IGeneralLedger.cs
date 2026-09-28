using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// The general ledger that approved reserve changes are posted to (FRS §6.5). In this assessment the
/// ledger is simulated (Infrastructure); the audit row is the visible result. A real client would send
/// <paramref name="idempotencyKey"/> to the ledger, so that a repeated call can never post twice: that is
/// what the key Reserve:{ReserveId}:Change:{Seq} is for (BR-R-06).
/// </summary>
public interface IGeneralLedger
{
    Task PostAsync(GlJournalEntry entry, string idempotencyKey, CancellationToken cancellationToken);
}
