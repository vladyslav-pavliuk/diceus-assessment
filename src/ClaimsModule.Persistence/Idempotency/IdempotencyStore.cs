using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Idempotency;

/// <summary>
/// Idempotency-Key bookkeeping (D-24). Every statement runs on its own, outside the command's unit of
/// work: the placeholder row must be committed before the command starts, so that a concurrent
/// duplicate finds it. The unique (UserId, Key) index decides the race: exactly one insert wins.
/// </summary>
internal sealed class IdempotencyStore(ClaimsDbContext dbContext, ITenantContext tenantContext, TimeProvider timeProvider) : IIdempotencyStore
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<IdempotencyClaim> BeginAsync(IdempotentRequest request, CancellationToken cancellationToken)
    {
        var organisationId = tenantContext.OrganisationId
            ?? throw new InvalidOperationException("An idempotent request needs a tenant scope.");

        var recordId = SequentialGuid.NewGuid();
        try
        {
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO [IdempotencyRecords] ([Id], [OrganisationId], [UserId], [Key], [Method], [Route], [RequestHash], [CreatedAt])
                VALUES ({recordId}, {organisationId}, {request.UserId}, {request.Key}, {request.Method}, {request.Route}, {request.RequestHash}, {timeProvider.GetUtcNow()})
                """,
                cancellationToken);

            return new IdempotencyClaim.Started(recordId);
        }
        catch (SqlException exception) when (exception.Number is PrimaryKeyViolation or UniqueIndexViolation)
        {
            // The key was seen before. Decide from the stored row.
        }

        var existing = await dbContext.Set<IdempotencyRecord>().AsNoTracking()
            .SingleAsync(record => record.UserId == request.UserId && record.Key == request.Key, cancellationToken);

        // Our own insert, retried by the execution strategy after a transient error that hid its success.
        if (existing.Id == recordId)
        {
            return new IdempotencyClaim.Started(recordId);
        }

        if (existing.Method != request.Method || existing.Route != request.Route || existing.RequestHash != request.RequestHash)
        {
            return new IdempotencyClaim.Mismatch();
        }

        return existing is { CompletedAt: not null, StatusCode: { } statusCode }
            ? new IdempotencyClaim.Replay(new StoredResponse(statusCode, existing.ResponseBody ?? string.Empty, existing.ResponseLocation))
            : new IdempotencyClaim.InProgress();
    }

    public Task CompleteAsync(Guid recordId, StoredResponse response, CancellationToken cancellationToken) =>
        dbContext.Set<IdempotencyRecord>()
            .Where(record => record.Id == recordId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.StatusCode, response.StatusCode)
                    .SetProperty(record => record.ResponseBody, response.Body)
                    .SetProperty(record => record.ResponseLocation, response.Location)
                    .SetProperty(record => record.CompletedAt, timeProvider.GetUtcNow()),
                cancellationToken);

    public Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken) =>
        dbContext.Set<IdempotencyRecord>().Where(record => record.Id == recordId).ExecuteDeleteAsync(cancellationToken);
}
