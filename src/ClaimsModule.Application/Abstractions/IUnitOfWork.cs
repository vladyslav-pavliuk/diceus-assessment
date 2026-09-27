namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Coordinates one command as one database transaction (CLAUDE.md rule 4). Handlers never call
/// SaveChanges; the UnitOfWorkBehavior (Phase 3, with the first command) wraps each command handler
/// in <see cref="ExecuteInTransactionAsync{TResult}"/>.
/// <para>
/// Commit sequence: dispatch before-commit domain events (audit rows, same transaction) →
/// SaveChanges → COMMIT → dispatch after-commit domain events (Hangfire enqueue). The whole
/// operation runs inside the EF Core execution strategy, so a transient Azure SQL failure replays
/// the unit as a whole (ARCHITECTURE-PLAN §4).
/// </para>
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
