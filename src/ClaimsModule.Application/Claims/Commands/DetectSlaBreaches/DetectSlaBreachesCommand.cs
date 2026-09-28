using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Auditing;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.DetectSlaBreaches;

/// <summary>
/// The body of SlaMonitoringJob (FRS §12.2, D-01), for the current tenant: every Draft or Open claim not
/// updated for more than 48 hours gets one SLA_BREACH_DETECTED audit entry, unless it already got one in the
/// last 24 hours. The claim row is never written: no status change (FRS §12.2), no UpdatedAt change (which
/// would reset the very clock this job measures), no RowVer change (no spurious 409 for a user editing the
/// claim). Returns the number of breaches recorded.
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
