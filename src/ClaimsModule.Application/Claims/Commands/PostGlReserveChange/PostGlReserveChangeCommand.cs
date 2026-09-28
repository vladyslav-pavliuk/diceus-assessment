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
/// Posts once however often it runs: the compare-and-set to Posted comes first, and its row lock makes a concurrent
/// copy wait and then change nothing. A later failure rolls the compare-and-set back too, so a retry starts from Pending.
/// </summary>
/// <param name="JobId">Null outside Hangfire.</param>
public sealed record PostGlReserveChangeCommand(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey, string? JobId)
    : ICommand<GlPostingOutcome>;

public enum GlPostingOutcome
{
    Posted = 1,

    /// <summary>Already posted, failed, not approved or unknown; nothing was written.</summary>
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
