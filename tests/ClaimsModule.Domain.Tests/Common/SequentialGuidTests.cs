using System.Data.SqlTypes;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Tests.Common;

public sealed class SequentialGuidTests
{
    /// <summary>D-30: ids generated in sequence must sort in the same order in SQL Server.</summary>
    [Fact]
    public void D_30_Sequential_guids_increase_in_SQL_Server_order()
    {
        var ids = Enumerable.Range(0, 1_000).Select(_ => SequentialGuid.NewGuid()).ToList();

        ids.Select(id => new SqlGuid(id)).Order().Select(sqlGuid => sqlGuid.Value).ShouldBe(ids);
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void D_30_Entities_get_their_id_at_construction()
    {
        TestData.DraftClaim().Id.ShouldNotBe(Guid.Empty);
    }
}
