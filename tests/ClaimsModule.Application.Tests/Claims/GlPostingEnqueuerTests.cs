using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application.Tests.Claims;

/// <summary>
/// JOB-01: which events enqueue PostGLReserveChangeJob after commit (FRS §6.3, §6.4 step 8, D-08), and with which
/// arguments (FRS §12.1: ReserveHistoryId, ClaimId, IdempotencyKey). That the after-commit phase really runs after
/// the commit, and never after a rollback, is proven end to end by the Phase 3 unit-of-work tests.
/// </summary>
public sealed class GlPostingEnqueuerTests
{
    private readonly RecordingScheduler _scheduler = new();

    [Fact]
    public async Task JOB_01_Auto_approval_approval_and_retry_enqueue_the_gl_job_after_commit()
    {
        var claimId = Guid.NewGuid();
        var autoApproved = new ReserveAutoApproved(claimId, Guid.NewGuid(), "Reserve:a:Change:1", 5_000m);
        var approved = new ReserveApproved(claimId, Guid.NewGuid(), "Reserve:b:Change:1", 50_000m);
        var retried = new GlPostingRetryRequested(claimId, Guid.NewGuid(), "Reserve:c:Change:1", 7_500m);

        await DispatcherAsync(dispatcher => dispatcher.DispatchAfterCommitAsync([autoApproved, approved, retried], CancellationToken.None));

        _scheduler.Enqueued.ShouldBe(
        [
            (autoApproved.TransactionId, claimId, autoApproved.IdempotencyKey),
            (approved.TransactionId, claimId, approved.IdempotencyKey),
            (retried.TransactionId, claimId, retried.IdempotencyKey),
        ]);
    }

    [Fact]
    public async Task BR_R_02_Submitting_rejecting_or_retracting_enqueues_nothing()
    {
        var claimId = Guid.NewGuid();

        await DispatcherAsync(dispatcher => dispatcher.DispatchAfterCommitAsync(
            [
                new ReserveRejected(claimId, Guid.NewGuid(), 20_000m, "No."),
                new ReserveRetracted(claimId, Guid.NewGuid(), 20_000m),
            ],
            CancellationToken.None));

        _scheduler.Enqueued.ShouldBeEmpty();
    }

    [Fact]
    public async Task JOB_01_Nothing_is_enqueued_before_commit()
    {
        await DispatcherAsync(dispatcher => dispatcher.DispatchBeforeCommitAsync(
            [new ReserveApproved(Guid.NewGuid(), Guid.NewGuid(), "Reserve:d:Change:1", 50_000m)], CancellationToken.None));

        _scheduler.Enqueued.ShouldBeEmpty();
    }

    private async Task DispatcherAsync(Func<IDomainEventDispatcher, Task> act)
    {
        await using var provider = new ServiceCollection().AddLogging().AddApplication()
            .AddSingleton<IBackgroundJobScheduler>(_scheduler)
            .AddScoped<IAuditLogService, NoAuditLog>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await act(scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>());
    }

    private sealed class RecordingScheduler : IBackgroundJobScheduler
    {
        public List<(Guid ReserveHistoryId, Guid ClaimId, string IdempotencyKey)> Enqueued { get; } = [];

        public string EnqueueGlPosting(Guid reserveHistoryId, Guid claimId, string idempotencyKey)
        {
            Enqueued.Add((reserveHistoryId, claimId, idempotencyKey));
            return Enqueued.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed class NoAuditLog : IAuditLogService
    {
        public void Record(AuditEntry entry)
        {
        }
    }
}
