using ClaimsModule.Domain.Policies;

namespace ClaimsModule.Application.Policies;

/// <summary>A simulated policy (FRS §9.10) as the FNOL policy lookup shows it (FRS §10.3, brief §3.3.2).</summary>
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

/// <summary>GET /api/policies/{id}/coverage (brief §3.3.2, D-08): the coverage types shown during FNOL.</summary>
public sealed record PolicyCoverageDto(Guid PolicyId, string PolicyNumber, IReadOnlyList<string> CoverageTypes);
