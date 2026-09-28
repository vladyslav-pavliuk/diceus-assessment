namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// One command, one transaction: before-commit handlers → SaveChanges → COMMIT → after-commit handlers.
/// Runs inside the EF execution strategy, so a transient SQL failure replays the whole unit.
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
