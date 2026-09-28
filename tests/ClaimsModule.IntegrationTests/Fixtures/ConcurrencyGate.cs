using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims.Events;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// Forces the worst interleaving of concurrent commands: every participant has loaded the claim and changed
/// it in memory before any of them saves. The hook is a before-commit handler, which the unit of work runs
/// after the command handler and before SaveChanges (ARCHITECTURE-PLAN §2.5). Inert until a test arms it;
/// after <c>participants</c> arrivals it disarms, so a replayed unit passes straight through.
/// </summary>
public sealed class ConcurrencyGate
{
    private readonly object _lock = new();
    private int _expected;
    private int _arrived;
    private TaskCompletionSource? _allArrived;
    private TaskCompletionSource _firstArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the first participant is waiting at the gate (it has loaded and changed the claim).</summary>
    public Task FirstArrival => _firstArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));

    public void Arm(int participants)
    {
        lock (_lock)
        {
            _expected = participants;
            _arrived = 0;
            _allArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _firstArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Disarm()
    {
        lock (_lock)
        {
            _allArrived?.TrySetResult();
            _allArrived = null;
        }
    }

    public Task ArriveAsync()
    {
        Task wait;
        lock (_lock)
        {
            if (_allArrived is not { } allArrived)
            {
                return Task.CompletedTask;
            }

            wait = allArrived.Task;
            _firstArrived.TrySetResult();
            if (++_arrived == _expected)
            {
                allArrived.TrySetResult();
                _allArrived = null;
            }
        }

        // A participant that never arrives is a broken test, not a hang.
        return wait.WaitAsync(TimeSpan.FromSeconds(15));
    }
}

/// <summary>Registered for the events of the commands the concurrency tests race.</summary>
internal sealed class ConcurrencyGateHandler(ConcurrencyGate gate) :
    IBeforeCommitHandler<ReserveApproved>,
    IBeforeCommitHandler<ReserveTransactionSubmitted>
{
    public Task HandleAsync(ReserveApproved domainEvent, CancellationToken cancellationToken) => gate.ArriveAsync();

    public Task HandleAsync(ReserveTransactionSubmitted domainEvent, CancellationToken cancellationToken) => gate.ArriveAsync();
}
