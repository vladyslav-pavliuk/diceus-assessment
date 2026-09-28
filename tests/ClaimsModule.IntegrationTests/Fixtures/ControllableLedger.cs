using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// The general ledger as the tests need it: records every posting, can be told to fail, and can hold a posting
/// open (inside the GL job's transaction, with the ReserveHistory row locked) until the test releases it.
/// Replaces the simulated ledger in the test hosts; reset by each test that changes it.
/// </summary>
public sealed class ControllableLedger : IGeneralLedger
{
    private readonly object _gate = new();
    private readonly List<(GlJournalEntry Entry, string IdempotencyKey)> _postings = [];
    private int _failuresLeft;
    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _release;
    private TaskCompletionSource? _held;

    public IReadOnlyList<(GlJournalEntry Entry, string IdempotencyKey)> Postings
    {
        get
        {
            lock (_gate)
            {
                return _postings.ToList();
            }
        }
    }

    /// <summary>The next <paramref name="count"/> postings throw; <see cref="int.MaxValue"/> for "always".</summary>
    public void FailNext(int count)
    {
        lock (_gate)
        {
            _failuresLeft = count;
        }
    }

    /// <summary>The next posting signals the returned task when it starts, then waits for <see cref="ReleaseHeldPosting"/>.</summary>
    public Task HoldNextPosting()
    {
        lock (_gate)
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }
    }

    public void ReleaseHeldPosting()
    {
        lock (_gate)
        {
            _held?.TrySetResult();
            _held = null;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _postings.Clear();
            _failuresLeft = 0;
            _held?.TrySetResult();
            _release?.TrySetResult();
            _entered = null;
            _release = null;
            _held = null;
        }
    }

    public async Task PostAsync(GlJournalEntry entry, string idempotencyKey, CancellationToken cancellationToken)
    {
        Task? hold = null;
        lock (_gate)
        {
            if (_failuresLeft > 0)
            {
                _failuresLeft = _failuresLeft == int.MaxValue ? int.MaxValue : _failuresLeft - 1;
                throw new InvalidOperationException("Test ledger failure.");
            }

            if (_entered is { } entered && _release is { } release)
            {
                _entered = null;
                _release = null;
                _held = release;
                entered.TrySetResult();
                hold = release.Task;
            }
        }

        if (hold is not null)
        {
            await hold.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }

        lock (_gate)
        {
            _postings.Add((entry, idempotencyKey));
        }
    }
}
