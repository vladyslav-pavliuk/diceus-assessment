using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Simulated here. A real client would pass the idempotency key to the ledger, so a repeated call never
/// posts twice (BR-R-06).
/// </summary>
public interface IGeneralLedger
{
    Task PostAsync(GlJournalEntry entry, string idempotencyKey, CancellationToken cancellationToken);
}
