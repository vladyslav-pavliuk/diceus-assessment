using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Persistence;

/// <summary>FRS §15.1/§15.2 column conventions, checked on the EF model every table is generated from.</summary>
[Collection(ApiCollection.Name)]
public sealed class ModelConventionTests(ApiFixture fixture)
{
    [Fact]
    public void CONV_01_All_guid_primary_keys_default_to_NEWSEQUENTIALID()
    {
        Tables()
            .Select(entity => entity.FindPrimaryKey()!)
            .Where(key => key.Properties is [{ ClrType: var type }] && type == typeof(Guid))
            .Where(key => key.Properties[0].GetDefaultValueSql() != "NEWSEQUENTIALID()")
            .Select(key => key.DeclaringEntityType.GetTableName())
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_02_All_decimals_are_19_4()
    {
        Properties()
            .Where(property => Unwrap(property.ClrType) == typeof(decimal) && property.GetColumnType() != "decimal(19,4)")
            .Select(Describe)
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_03_All_timestamps_are_datetimeoffset_7()
    {
        Properties().Where(property => Unwrap(property.ClrType) == typeof(DateTime)).Select(Describe).ShouldBeEmpty();
        Properties()
            .Where(property => Unwrap(property.ClrType) == typeof(DateTimeOffset) && property.GetColumnType() != "datetimeoffset(7)")
            .Select(Describe)
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_04_Enums_are_stored_as_nvarchar_text()
    {
        var enums = Properties().Where(property => Unwrap(property.ClrType).IsEnum).ToList();
        enums.Count.ShouldBeGreaterThan(15);

        enums.Where(property => property.GetColumnType() is not ("nvarchar(50)" or "nvarchar(20)" or "nvarchar(100)"))
            .Select(Describe)
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_05_Every_business_table_has_soft_delete_audit_and_tenant_columns()
    {
        string[] required = ["IsDeleted", "DeletedAt", "CreatedAt", "UpdatedAt", "UserCreated", "UserModified", "OrganisationId"];
        // D-14: the tenant, global configuration and bookkeeping tables (audit log, counters, idempotency records).
        string[] exempt = ["Organisations", "ClaimStatusTransitions", "ClaimAuditLog", "ClaimNumberCounters", "IdempotencyRecords"];

        Tables()
            .Where(entity => !exempt.Contains(entity.GetTableName()))
            .SelectMany(entity => required.Where(column => entity.FindProperty(column) is null).Select(column => $"{entity.GetTableName()}.{column}"))
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_08_RowVer_is_on_the_aggregate_roots()
    {
        Tables()
            .Where(entity => entity.FindProperty("RowVer") is { IsConcurrencyToken: true, IsNullable: false })
            .Select(entity => entity.GetTableName())
            .ShouldBe(["ClaimReserveComponents", "Claims"], ignoreOrder: true);
    }

    [Fact]
    public void CONV_05_D_14_The_audit_log_has_no_update_or_delete_columns()
    {
        var auditLog = Tables().Single(entity => entity.GetTableName() == "ClaimAuditLog");

        string[] forbidden = ["IsDeleted", "DeletedAt", "UpdatedAt", "UserModified"];
        forbidden.Where(column => auditLog.FindProperty(column) is not null).ShouldBeEmpty();
        auditLog.FindProperty("OrganisationId").ShouldNotBeNull();
    }

    private IReadOnlyList<IEntityType> Tables()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().GetService<IDesignTimeModel>().Model.GetEntityTypes().ToList();
    }

    private IEnumerable<IProperty> Properties() => Tables().SelectMany(entity => entity.GetProperties());

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static string Describe(IProperty property) =>
        $"{property.DeclaringType.GetTableName()}.{property.GetColumnName()} ({property.GetColumnType()})";
}
