using ClaimsModule.Domain.Policies;

namespace ClaimsModule.Application.Policies;

public sealed record PolicyDto
{
    public required Guid Id { get; init; }

    public required string PolicyNumber { get; init; }

    public required string ClientName { get; init; }

    public required DateOnly EffectiveDate { get; init; }

    public required DateOnly ExpirationDate { get; init; }

    public required PolicyStatus Status { get; init; }

    public required IReadOnlyList<string> CoverageTypes { get; init; }
}

public sealed record PolicyCoverageDto(Guid PolicyId, string PolicyNumber, IReadOnlyList<string> CoverageTypes);
