using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Auditing;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Claims.Commands.PostGlReserveChange;

/// <summary>
/// The body of PostGLReserveChangeJob (FRS §6.5, §12.1): posts one approved reserve change to the general
/// ledger exactly once, however many times it runs and however many copies run at once.
/// <para>
/// One unit of work (the UnitOfWorkBehavior's transaction):
/// <list type="number">
/// <item>compare-and-set Pending → Posted in one UPDATE (<see cref="IGlPostingStore.TryMarkPostedAsync"/>);
/// 0 rows changed → nothing to do, and nothing is written;</item>
/// <item>post the journal to the ledger, with the idempotency key;</item>
/// <item>stage GL_POSTING_SIMULATED;</item>
/// <item>commit. Any failure in 2–4 rolls back 1 as well, so a retry starts from Pending (R7).</item>
/// </list>
/// The row lock taken in step 1 makes a concurrent copy wait, then change 0 rows (ARCHITECTURE-PLAN §6.1 R6).
/// </para>
/// </summary>
/// <param name="JobId">The Hangfire job id, stored in ReserveHistory.PostingJobId (FRS §12.1); null outside Hangfire.</param>
public sealed record PostGlReserveChangeCommand(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey, string? JobId)
    : ICommand<GlPostingOutcome>;

public enum GlPostingOutcome
{
    /// <summary>This run posted the change and wrote GL_POSTING_SIMULATED.</summary>
    Posted = 1,

    /// <summary>Nothing was pending for these arguments (already posted, failed, not approved, or unknown); no write.</summary>
    NothingToPost,
}

internal sealed class PostGlReserveChangeCommandHandler(
    IGlPostingStore postings,
    IGeneralLedger ledger,
    IAuditLogService auditLog,
    ILogger<PostGlReserveChangeCommandHandler> logger) : IRequestHandler<PostGlReserveChangeCommand, GlPostingOutcome>
{
    public async Task<GlPostingOutcome> Handle(PostGlReserveChangeCommand request, CancellationToken cancellationToken)
    {
        var posted = await postings.TryMarkPostedAsync(
            new GlPostingRequest(request.ReserveHistoryId, request.ClaimId, request.IdempotencyKey), request.JobId, cancellationToken);

        if (posted is null)
        {
            logger.LogInformation("GL posting {IdempotencyKey}: nothing pending to post", request.IdempotencyKey);
            return GlPostingOutcome.NothingToPost;
        }

        var journal = GlJournalEntry.ForReserveChange(posted.Amount);
        await ledger.PostAsync(journal, posted.IdempotencyKey, cancellationToken);

        // FRS §6.5: "DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = {reserveAmount}".
        auditLog.Record(new AuditEntry(
            posted.ClaimId,
            AuditEventTypes.GlPostingSimulated,
            $"GL posting simulated: {journal.Lines}, Amount = {AuditValues.Money(journal.Amount)}.",
            NewValue: AuditValues.ToJson(new
            {
                PostingStatus = PostingStatus.Posted,
                Journal = new { journal.DebitAccount, journal.CreditAccount, journal.Amount },
                ReserveAmount = posted.Amount,
                posted.Component,
                posted.ChangeSequence,
                posted.IdempotencyKey,
                request.JobId,
            }),
            RelatedEntityId: posted.ReserveHistoryId,
            RelatedEntityType: AuditValues.ReserveTransactionEntity));

        return GlPostingOutcome.Posted;
    }
}
