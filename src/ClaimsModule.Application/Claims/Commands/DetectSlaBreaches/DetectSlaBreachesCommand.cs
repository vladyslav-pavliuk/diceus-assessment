using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Auditing;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;

/// <summary>
/// Writes audit rows only. Touching the claim would reset the UpdatedAt clock this job measures and cause spurious
/// 409s for users (D-01). Returns the number of breaches recorded.
/// </summary>
public sealed record DetectSlaBreachesCommand : ICommand<int>;

internal sealed class DetectSlaBreachesCommandHandler(
    ISlaMonitoringQueries queries,
    IAuditLogService auditLog,
    TimeProvider timeProvider) : IRequestHandler<DetectSlaBreachesCommand, int>
{
    public async Task<int> Handle(DetectSlaBreachesCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var breached = await queries.ListBreachCandidatesAsync(
            SlaPolicy.StaleBefore(now), SlaPolicy.RepeatSuppressedAfter(now), cancellationToken);

        foreach (var claim in breached)
        {
            auditLog.Record(new AuditEntry(
                claim.ClaimId,
                AuditEventTypes.SlaBreachDetected,
                SlaPolicy.BreachDescription,
                NewValue: AuditValues.ToJson(new
                {
                    claim.Status,
                    claim.LastUpdatedAt,
                    HoursSinceUpdate = Math.Floor((now - claim.LastUpdatedAt).TotalHours),
                })));
        }

        return breached.Count;
    }
}
