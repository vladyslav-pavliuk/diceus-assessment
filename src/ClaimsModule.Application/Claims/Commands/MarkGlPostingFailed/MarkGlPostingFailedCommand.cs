using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Auditing;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.MarkGlPostingFailed;

/// <summary>
/// Own unit of work, because the failed attempt rolled back. Compare-and-set on Pending, so it never overwrites a
/// posting another run completed (D-35). Returns whether it changed the row.
/// </summary>
public sealed record MarkGlPostingFailedCommand(
    Guid ReserveHistoryId,
    Guid ClaimId,
    string IdempotencyKey,
    string? JobId,
    int Attempts,
    string Reason) : ICommand<bool>;

internal sealed class MarkGlPostingFailedCommandHandler(IGlPostingStore postings, IAuditLogService auditLog)
    : IRequestHandler<MarkGlPostingFailedCommand, bool>
{
    public async Task<bool> Handle(MarkGlPostingFailedCommand request, CancellationToken cancellationToken)
    {
        var failed = await postings.TryMarkFailedAsync(
            new GlPostingRequest(request.ReserveHistoryId, request.ClaimId, request.IdempotencyKey), request.JobId, cancellationToken);

        if (failed is null)
        {
            return false;
        }

        auditLog.Record(new AuditEntry(
            failed.ClaimId,
            AuditEventTypes.GlPostingFailed,
            $"GL posting of the reserve change of {AuditValues.Money(failed.Amount)} failed after {request.Attempts} attempts: {request.Reason}",
            OldValue: AuditValues.ToJson(new { PostingStatus = PostingStatus.Pending }),
            NewValue: AuditValues.ToJson(new
            {
                PostingStatus = PostingStatus.Failed,
                request.Reason,
                request.Attempts,
                failed.IdempotencyKey,
                request.JobId,
            }),
            RelatedEntityId: failed.ReserveHistoryId,
            RelatedEntityType: AuditValues.ReserveTransactionEntity));

        return true;
    }
}
