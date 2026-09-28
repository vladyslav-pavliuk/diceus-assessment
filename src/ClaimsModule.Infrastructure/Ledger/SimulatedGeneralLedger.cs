using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Ledger;

/// <summary>Logs the journal. Jobs:GlPosting:SimulateFailure makes every call fail, to demo the retry path (D-35).</summary>
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
