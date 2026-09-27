using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Persistence;

/// <summary>What the database and the SaveChanges interceptors guarantee, whatever the application code does.</summary>
[Collection(ApiCollection.Name)]
public sealed class DataIntegrityTests(ApiFixture fixture)
{
    private readonly TestDatabase _database = new(fixture);

    [Fact]
    public async Task DOM_02_Full_aggregate_round_trips_and_stays_usable()
    {
        var created = await _database.CreateClaimAsync(2097);
        var supervisor = await _database.SeededUserAsync("supervisor.casey");
        var handler = await _database.SeededUserAsync("handler.alex");
        var pendingId = await ChangeClaimAsync(created.Id, claim =>
            claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, null, 25_000m, "Repair estimate", handler, DateTimeOffset.UtcNow.AddMinutes(-1)).Transaction.Id);

        await ChangeClaimAsync(created.Id, claim =>
        {
            claim.ApproveReserveTransaction(pendingId, supervisor, DateTimeOffset.UtcNow.AddMinutes(-1));
            return 0;
        });

        var reloaded = await LoadClaimAsync(created.Id);
        reloaded.ClaimNumber.ShouldBe(created.ClaimNumber);
        reloaded.LossEvent.CauseOfLossCode.ShouldBe("COL-VEH-COL");
        reloaded.Parties.ShouldHaveSingleItem().IsActiveClaimant.ShouldBeTrue();
        reloaded.RiskObjects.ShouldHaveSingleItem().IsPrimary.ShouldBeTrue();
        var component = reloaded.ReserveComponents.ShouldHaveSingleItem();
        component.CurrentAmount.ShouldBe(25_000m);
        component.Transactions.ShouldHaveSingleItem().ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
    }

    [Fact]
    public async Task CONV_06_Audit_columns_are_populated_and_a_child_change_touches_the_claim()
    {
        var created = await _database.CreateClaimAsync(2097);
        var handler = await _database.SeededUserAsync("handler.alex");

        await using (var scope = await _database.TenantScopeAsync())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            var claim = await dbContext.Claims.AsNoTracking().SingleAsync(candidate => candidate.Id == created.Id);
            dbContext.Entry(claim).State.ShouldBe(EntityState.Detached);
            (await ShadowAsync<DateTimeOffset?>(dbContext, created.Id, "UpdatedAt")).ShouldBeNull();
            (await ShadowAsync<DateTimeOffset>(dbContext, created.Id, "CreatedAt")).Offset.ShouldBe(TimeSpan.Zero);
        }

        await ChangeClaimAsync(created.Id, claim => claim.AddParty(
            new PartyDetails(PartyRole.Witness, PartyType.Person, "Sam", "Lee", null, null, null, null), handler, DateTimeOffset.UtcNow));

        await using (var scope = await _database.TenantScopeAsync())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            (await ShadowAsync<DateTimeOffset?>(dbContext, created.Id, "UpdatedAt")).ShouldNotBeNull(); // the Claim row was touched (RowVer)
        }
    }

    [Fact]
    public async Task CONV_08_Concurrent_changes_to_one_claim_conflict_on_RowVer()
    {
        var created = await _database.CreateClaimAsync(2097);
        var handler = await _database.SeededUserAsync("handler.alex");

        await using var first = await _database.TenantScopeAsync();
        await using var second = await _database.TenantScopeAsync();
        var firstClaim = await first.ServiceProvider.GetRequiredService<IClaimRepository>().GetAsync(created.Id, CancellationToken.None);
        var secondClaim = await second.ServiceProvider.GetRequiredService<IClaimRepository>().GetAsync(created.Id, CancellationToken.None);

        // Different children of the same claim: without the Claim RowVer both would commit (write skew).
        firstClaim!.AddRiskObject(new RiskObjectDetails(AssetType.Equipment, "Pallet truck", null, null), handler, DateTimeOffset.UtcNow);
        secondClaim!.UpdateNotes("Called the depot manager.", handler);
        await first.ServiceProvider.GetRequiredService<ClaimsDbContext>().SaveChangesAsync();

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.ServiceProvider.GetRequiredService<ClaimsDbContext>().SaveChangesAsync());
    }

    [Fact]
    public async Task SEC_04_Other_tenants_rows_are_invisible_and_cannot_be_written()
    {
        var created = await _database.CreateClaimAsync(2097);
        var otherOrganisation = Guid.NewGuid();
        await using (var scope = _database.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.ExecuteSqlAsync(
                $"INSERT INTO [Organisations] ([Id], [Name], [CreatedAt]) VALUES ({otherOrganisation}, N'Other Insurer', SYSDATETIMEOFFSET())");
        }

        await using (var scope = await _database.TenantScopeAsync(otherOrganisation))
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            (await dbContext.Claims.AnyAsync(claim => claim.Id == created.Id)).ShouldBeFalse();
            (await dbContext.Policies.CountAsync()).ShouldBe(0);
            (await scope.ServiceProvider.GetRequiredService<IClaimRepository>().GetAsync(created.Id, CancellationToken.None)).ShouldBeNull();
        }

        await using (var scope = await _database.TenantScopeAsync())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            var policy = Policy.Create($"POL-X-{Guid.NewGuid():N}"[..20], "Other Client", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), PolicyStatus.Active, []);
            dbContext.Policies.Add(policy);
            dbContext.Entry(policy).Property("OrganisationId").CurrentValue = otherOrganisation;

            (await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync())).Message.ShouldContain("another organisation");
        }
    }

    [Fact]
    public async Task SEC_04_Without_a_tenant_nothing_is_visible_and_nothing_can_be_inserted()
    {
        await using var scope = _database.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        (await dbContext.Policies.CountAsync()).ShouldBe(0);
        dbContext.Policies.Add(Policy.Create("POL-NO-TENANT", "Nobody", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), PolicyStatus.Active, []));
        await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CONV_05_Deleted_rows_are_soft_deleted_and_filtered()
    {
        var number = $"POL-SD-{Guid.NewGuid():N}"[..20];
        await using (var scope = await _database.TenantScopeAsync())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            dbContext.Policies.Add(Policy.Create(number, "Soft Delete Ltd", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), PolicyStatus.Active, ["Property"]));
            await dbContext.SaveChangesAsync();
            dbContext.Policies.Remove(await dbContext.Policies.SingleAsync(policy => policy.PolicyNumber == number));
            await dbContext.SaveChangesAsync();
        }

        await using (var scope = await _database.TenantScopeAsync())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            (await dbContext.Policies.AnyAsync(policy => policy.PolicyNumber == number)).ShouldBeFalse();

            var row = await dbContext.Policies.IgnoreQueryFilters().Where(policy => policy.PolicyNumber == number)
                .Select(policy => new { IsDeleted = EF.Property<bool>(policy, "IsDeleted"), DeletedAt = EF.Property<DateTimeOffset?>(policy, "DeletedAt") })
                .SingleAsync();
            row.IsDeleted.ShouldBeTrue();
            row.DeletedAt.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task AUD_I3_Audit_rows_carry_tenant_correlation_id_actor_and_utc_time()
    {
        var created = await _database.CreateClaimAsync(2097);
        var correlationId = Guid.NewGuid();

        await using (var scope = _database.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<Infrastructure.Tenancy.TenantContext>().SetOrganisation(await _database.SeededOrganisationIdAsync());
            scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set(correlationId.ToString());
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                _ =>
                {
                    scope.ServiceProvider.GetRequiredService<IAuditLogService>().Record(
                        new AuditEntry(created.Id, AuditEventTypes.ClaimCreated, "Claim created.", NewValue: "{\"claimNumber\":\"x\"}"));
                    return Task.FromResult(0);
                },
                CancellationToken.None);
        }

        var entry = await SingleAuditEntryAsync(created.Id);
        entry.CorrelationId.ShouldBe(correlationId);
        entry.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
        entry.CreatedByUserId.ShouldBeNull(); // no signed-in user: the system actor (D-33)
    }

    [Fact]
    public async Task BR_A_01_Modifying_or_deleting_an_audit_entry_throws()
    {
        var created = await _database.CreateClaimAsync(2097);
        await RecordAuditAsync(created.Id);

        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var entry = await dbContext.ClaimAuditLog.SingleAsync(candidate => candidate.ClaimId == created.Id);

        dbContext.Entry(entry).Property(nameof(ClaimAuditLog.Description)).CurrentValue = "Rewritten history";
        (await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync())).Message.ShouldContain("append-only");

        dbContext.ChangeTracker.Clear();
        dbContext.ClaimAuditLog.Remove(await dbContext.ClaimAuditLog.SingleAsync(candidate => candidate.ClaimId == created.Id));
        (await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync())).Message.ShouldContain("append-only");
    }

    [Fact]
    public async Task BR_A_01_Db_trigger_blocks_raw_update_and_delete()
    {
        var created = await _database.CreateClaimAsync(2097);
        await RecordAuditAsync(created.Id);

        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        // Set-based statements never pass through the change tracker, so only the trigger can stop them.
        (await Should.ThrowAsync<SqlException>(() => dbContext.ClaimAuditLog.Where(entry => entry.ClaimId == created.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Description, "Rewritten"))))
            .Number.ShouldBe(51000);
        (await Should.ThrowAsync<SqlException>(() => dbContext.Database.ExecuteSqlAsync($"DELETE FROM [ClaimAuditLog] WHERE [ClaimId] = {created.Id}")))
            .Number.ShouldBe(51000);

        (await SingleAuditEntryAsync(created.Id)).Description.ShouldBe("Claim created.");
    }

    [Fact]
    public async Task RSV_04_Modifying_a_history_amount_throws()
    {
        var created = await _database.CreateClaimAsync(2097);
        var handler = await _database.SeededUserAsync("handler.alex");
        await ChangeClaimAsync(created.Id, claim => claim.SubmitReserveTransaction(ReserveComponentType.Expense, null, 900m, "Fee", handler, DateTimeOffset.UtcNow));

        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        var transaction = await dbContext.ReserveHistory.SingleAsync(row => row.ClaimId == created.Id);

        dbContext.Entry(transaction).Property(nameof(ReserveTransaction.Amount)).CurrentValue = 9_000_000m;

        (await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync())).Message.ShouldContain("immutable");
    }

    [Fact]
    public async Task RSV_06_Database_allows_one_pending_transaction_per_component()
    {
        var created = await _database.CreateClaimAsync(2097);
        var handler = await _database.SeededUserAsync("handler.alex");
        await ChangeClaimAsync(created.Id, claim => claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, null, 5_000m, "Opening", handler, DateTimeOffset.UtcNow));
        await ChangeClaimAsync(created.Id, claim => claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, null, 50_000m, "Increase", handler, DateTimeOffset.UtcNow));

        await using var scope = await _database.TenantScopeAsync();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();

        // Bypass the domain: turn the auto-approved row into a second pending row.
        (await Should.ThrowAsync<SqlException>(() => dbContext.ReserveHistory
            .Where(row => row.ClaimId == created.Id && row.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ApprovalStatus, ReserveApprovalStatus.PendingApproval))))
            .Message.ShouldContain("UX_ReserveHistory_ReserveComponentId_Pending");
    }

    private async Task<T> ChangeClaimAsync<T>(Guid claimId, Func<Claim, T> change)
    {
        await using var scope = await _database.TenantScopeAsync();
        var services = scope.ServiceProvider;
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            async cancellationToken => change((await services.GetRequiredService<IClaimRepository>().GetAsync(claimId, cancellationToken))!),
            CancellationToken.None);
    }

    private async Task<Claim> LoadClaimAsync(Guid claimId)
    {
        await using var scope = await _database.TenantScopeAsync();
        return (await scope.ServiceProvider.GetRequiredService<IClaimRepository>().GetAsync(claimId, CancellationToken.None))!;
    }

    private async Task RecordAuditAsync(Guid claimId)
    {
        await using var scope = await _database.TenantScopeAsync();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            _ =>
            {
                scope.ServiceProvider.GetRequiredService<IAuditLogService>().Record(new AuditEntry(claimId, AuditEventTypes.ClaimCreated, "Claim created."));
                return Task.FromResult(0);
            },
            CancellationToken.None);
    }

    private async Task<ClaimAuditLog> SingleAuditEntryAsync(Guid claimId)
    {
        await using var scope = await _database.TenantScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().ClaimAuditLog.AsNoTracking().SingleAsync(entry => entry.ClaimId == claimId);
    }

    private static Task<T> ShadowAsync<T>(ClaimsDbContext dbContext, Guid claimId, string column) =>
        dbContext.Claims.Where(claim => claim.Id == claimId).Select(claim => EF.Property<T>(claim, column)).SingleAsync();
}
