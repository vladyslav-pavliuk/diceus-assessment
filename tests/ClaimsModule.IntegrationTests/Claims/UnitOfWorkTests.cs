using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Claims;

/// <summary>
/// The command pipeline end to end (CLAUDE.md rules 4 and 5): one transaction per command, audit rows
/// before commit, after-commit handlers only after a commit, and read models without N+1 queries.
/// Each test builds its own host on the shared database, so the extra test handlers stay local.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class UnitOfWorkTests(ApiFixture fixture)
{
    private const string FailMarker = "FAIL-BEFORE-COMMIT";

    [Fact]
    public async Task AUD_I4_A_failing_command_leaves_no_claim_no_audit_row_and_no_used_claim_number()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IBeforeCommitHandler<ClaimCreated>, FailingHandler>()));
        var api = await ClaimsApi.SignInAsync(factory, "handler.alex");

        var before = await api.CreateClaimAsync(); // the failing handler ignores claims without the marker
        var auditRowsBefore = await AuditRowCountAsync();
        var body = await api.ValidFnolAsync();
        body["lossDescription"] = $"{FailMarker}: the audit handler throws after the rows were staged.";

        var response = await api.PostClaimAsync(body);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await AuditRowCountAsync()).ShouldBe(auditRowsBefore);

        await using (var scope = await new TestDatabase(fixture).TenantScopeAsync())
        {
            (await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Set<LossEvent>()
                .AnyAsync(lossEvent => lossEvent.LossDescription.StartsWith(FailMarker))).ShouldBeFalse();
        }

        var after = await api.CreateClaimAsync();
        SequenceOf(after.ClaimNumber).ShouldBe(SequenceOf(before.ClaimNumber) + 1); // the rolled-back number was not consumed (D-10)
    }

    [Fact]
    public async Task CONV_15_After_commit_handlers_run_after_the_commit_and_never_after_a_rollback()
    {
        var observations = new ConcurrentBag<(Guid ClaimId, int VisibleRows)>();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(observations);
            services.AddScoped<IAfterCommitHandler<ClaimCreated>, CommitObserver>();
            services.AddScoped<IBeforeCommitHandler<ClaimCreated>, FailingHandler>();
        }));
        var api = await ClaimsApi.SignInAsync(factory, "handler.alex");

        var committed = await api.CreateClaimAsync();
        var body = await api.ValidFnolAsync();
        body["lossDescription"] = $"{FailMarker}: rolled back, so nothing may run after commit.";
        (await api.PostClaimAsync(body)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        // Exactly one observation, and the claim row was already committed when the handler ran.
        observations.ShouldHaveSingleItem().ShouldBe((committed.Id, 1));
    }

    [Fact]
    public async Task API_02_List_runs_a_fixed_number_of_queries_whatever_the_page_size()
    {
        var counter = new CommandCounter();
        await using var factory = CountingFactory(counter);
        var api = await ClaimsApi.SignInAsync(factory, "handler.alex");
        for (var i = 0; i < 3; i++)
        {
            await api.CreateClaimAsync();
        }

        var small = await CountAsync(counter, () => api.Client.GetAsync("/api/claims?pageSize=1"));
        var large = await CountAsync(counter, () => api.Client.GetAsync("/api/claims?pageSize=100"));

        small.ShouldBe(2); // COUNT + one page query, with names, totals and the SLA flag as subqueries
        large.ShouldBe(small);
    }

    [Fact]
    public async Task API_03_Detail_runs_a_fixed_number_of_queries_whatever_the_number_of_children()
    {
        var counter = new CommandCounter();
        await using var factory = CountingFactory(counter);
        var api = await ClaimsApi.SignInAsync(factory, "handler.alex");

        var small = await api.CreateClaimAsync();
        var bigBody = await api.ValidFnolAsync();
        bigBody["parties"] = new JsonArray(Enumerable.Range(0, 6).Select(_ => (JsonNode)ClaimsApi.ClaimantPerson()).ToArray());
        bigBody["initialReserve"] = new JsonObject { ["component"] = "Expense", ["amount"] = 900m };
        var big = await api.CreateClaimAsync(bigBody);

        var smallCount = await CountAsync(counter, () => api.Client.GetAsync($"/api/claims/{small.Id}"));
        var bigCount = await CountAsync(counter, () => api.Client.GetAsync($"/api/claims/{big.Id}"));

        bigCount.ShouldBe(smallCount);
        smallCount.ShouldBe(7); // header + parties + risk objects + issues + reserve components + documents + recent audit
    }

    private WebApplicationFactory<Program> CountingFactory(CommandCounter counter) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<ClaimsDbContext>(options => options.AddInterceptors(counter))));

    private static async Task<int> CountAsync(CommandCounter counter, Func<Task<HttpResponseMessage>> request)
    {
        counter.Reset();
        var response = await request();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return counter.Count;
    }

    private async Task<int> AuditRowCountAsync()
    {
        await using var scope = await new TestDatabase(fixture).TenantScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().ClaimAuditLog.CountAsync();
    }

    private static int SequenceOf(string claimNumber) => int.Parse(claimNumber[^7..], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Throws for the claim whose loss description carries the marker, after the other handlers staged its audit rows.</summary>
    private sealed class FailingHandler(ClaimsDbContext dbContext) : IBeforeCommitHandler<ClaimCreated>
    {
        public Task HandleAsync(ClaimCreated domainEvent, CancellationToken cancellationToken) =>
            dbContext.ChangeTracker.Entries<LossEvent>().Any(entry =>
                entry.Entity.ClaimId == domainEvent.ClaimId && entry.Entity.LossDescription.StartsWith(FailMarker, StringComparison.Ordinal))
                ? throw new NotSupportedException("Simulated failure after the audit rows were staged.")
                : Task.CompletedTask;
    }

    /// <summary>Counts the claim row from a separate connection that skips locked rows: 1 only once it is committed.</summary>
    private sealed class CommitObserver(ClaimsDbContext dbContext, ConcurrentBag<(Guid, int)> observations) : IAfterCommitHandler<ClaimCreated>
    {
        public async Task HandleAsync(ClaimCreated domainEvent, CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(dbContext.Database.GetConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT COUNT(*) FROM [Claims] WITH (READPAST) WHERE [Id] = @id", connection);
            command.Parameters.AddWithValue("@id", domainEvent.ClaimId);
            observations.Add((domainEvent.ClaimId, (int)(await command.ExecuteScalarAsync(cancellationToken))!));
        }
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }
}
