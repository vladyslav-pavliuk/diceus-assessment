using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.ClaimNumbers;

/// <summary>
/// Gap-free, duplicate-free claim numbers (FRS §5.3, BR-C-04, D-10):
/// <c>UPDATE … SET LastValue = LastValue + 1 OUTPUT INSERTED.LastValue</c> on the (organisation, year)
/// counter row, inside the claim-creation transaction.
/// <list type="bullet">
/// <item>No duplicates: the UPDATE takes an exclusive lock on the row, so concurrent creates for the
/// same organisation and year wait for each other until commit.</item>
/// <item>No gaps: if the claim's transaction rolls back, the increment rolls back with it. A SQL
/// SEQUENCE would not: it hands out values outside the transaction and loses its cache on restart.</item>
/// </list>
/// The first claim of a year finds no row and inserts (organisation, year, 1). If two such inserts
/// race, the loser gets a primary-key violation once the winner commits and retries the UPDATE.
/// </summary>
internal sealed class ClaimNumberGenerator(ClaimsDbContext dbContext, ITenantContext tenantContext) : IClaimNumberGenerator
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<ClaimNumber> NextAsync(int year, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Claim numbers must be drawn inside the claim-creation transaction (D-10).");
        }

        var organisationId = tenantContext.OrganisationId
            ?? throw new InvalidOperationException("Claim numbers are per organisation; no tenant scope is set.");

        var next = await IncrementAsync(organisationId, year, cancellationToken);
        if (next is null)
        {
            try
            {
                await dbContext.Database.ExecuteSqlAsync(
                    $"INSERT INTO [ClaimNumberCounters] ([OrganisationId], [Year], [LastValue]) VALUES ({organisationId}, {year}, 1)",
                    cancellationToken);
                next = 1;
            }
            catch (SqlException exception) when (exception.Number is PrimaryKeyViolation or UniqueIndexViolation)
            {
                // Another transaction created this year's row first; it exists now.
                next = await IncrementAsync(organisationId, year, cancellationToken)
                    ?? throw new InvalidOperationException("The claim-number counter row disappeared.", exception);
            }
        }

        return ClaimNumber.Create(year, next.Value);
    }

    private async Task<int?> IncrementAsync(Guid organisationId, int year, CancellationToken cancellationToken)
    {
        var values = await dbContext.Database
            .SqlQuery<int>(
                $"UPDATE [ClaimNumberCounters] SET [LastValue] = [LastValue] + 1 OUTPUT INSERTED.[LastValue] AS [Value] WHERE [OrganisationId] = {organisationId} AND [Year] = {year}")
            .ToListAsync(cancellationToken);

        return values.Count == 0 ? null : values[0];
    }
}
