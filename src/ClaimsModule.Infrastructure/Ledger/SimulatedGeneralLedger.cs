using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Ledger;

/// <summary>
/// Stands in for the general ledger (FRS §6.5 "simulates what in production would be a real double-entry
/// accounting transaction"): it logs the journal as a structured entry. With Jobs:GlPosting:SimulateFailure
/// it fails every call, which drives the retry and failure path in a demo (D-35).
/// </summary>
internal sealed class SimulatedGeneralLedger(IOptionsMonitor<JobsOptions> options, ILogger<SimulatedGeneralLedger> logger) : IGeneralLedger
{
    public Task PostAsync(GlJournalEntry entry, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (options.CurrentValue.GlPosting.SimulateFailure)
        {
            throw new GeneralLedgerUnavailableException("The general ledger is unavailable (simulated: Jobs:GlPosting:SimulateFailure is on).");
        }

        logger.LogInformation(
            "GL journal {IdempotencyKey}: DR {DebitAccount} / CR {CreditAccount} {Amount}",
            idempotencyKey,
            entry.DebitAccount,
            entry.CreditAccount,
            entry.Amount);

        return Task.CompletedTask;
    }
}

public sealed class GeneralLedgerUnavailableException(string message) : Exception(message);
