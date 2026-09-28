using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ClaimsModule.Persistence.Interceptors;

/// <summary>
/// Refuses changes the domain never makes, so a future bug fails loudly instead of rewriting history: audit rows are
/// append-only (D-14), and ReserveHistory amounts never change and rows are never deleted (D-22).
/// </summary>
internal sealed class ImmutableRowsInterceptor : SaveChangesInterceptor
{
    private static readonly string[] ImmutableReserveColumns =
    [
        nameof(ReserveTransaction.ReserveComponentId),
        nameof(ReserveTransaction.ClaimId),
        nameof(ReserveTransaction.TransactionType),
        nameof(ReserveTransaction.Amount),
        nameof(ReserveTransaction.PreviousBalance),
        nameof(ReserveTransaction.NewBalance),
        nameof(ReserveTransaction.ChangeSequence),
        nameof(ReserveTransaction.ChangeReason),
        nameof(ReserveTransaction.SubmittedByUserId),
        nameof(ReserveTransaction.IdempotencyKey),
        ShadowColumns.IsDeleted,
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Guard(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.Entity)
            {
                case ClaimAuditLog when entry.State is EntityState.Modified or EntityState.Deleted:
                    throw new InvalidOperationException("ClaimAuditLog is append-only (BR-A-01): rows can be added, never changed or deleted.");

                case ReserveTransaction when entry.State == EntityState.Deleted:
                    throw new InvalidOperationException("ReserveHistory rows are never deleted (FRS §6.6).");

                case ReserveTransaction when entry.State == EntityState.Modified:
                    var changed = ImmutableReserveColumns.Where(column => entry.Property(column).IsModified).ToList();
                    if (changed.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"ReserveHistory amounts are immutable (FRS §6.6, D-22); attempted to change {string.Join(", ", changed)}. Insert a new transaction instead.");
                    }

                    break;
            }
        }
    }
}
