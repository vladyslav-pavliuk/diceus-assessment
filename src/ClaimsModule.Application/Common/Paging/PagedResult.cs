namespace ClaimsModule.Application.Common.Paging;

/// <summary>One page of a list plus the metadata a paginated table needs (FRS §11.1 "total claim count", D-29).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>A 1-based page request. Validated by the query validators before it reaches the database.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}
