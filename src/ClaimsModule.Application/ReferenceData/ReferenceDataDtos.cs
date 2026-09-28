using AutoMapper;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.ReferenceData;

public sealed record CauseOfLossCodeDto(string Code, string Name, PerilCategory PerilCategory, int SortOrder);

public sealed record ClaimStatusDto(ClaimStatus Status, IReadOnlyList<ClaimStatusTransitionDto> Transitions);

/// <summary><see cref="IsSystemOnly"/> rows cannot be requested, so a status menu leaves them out.</summary>
public sealed record ClaimStatusTransitionDto(
    ClaimStatus FromStatus,
    ClaimStatus ToStatus,
    UserRole? MinimumRole,
    bool RequiresReason,
    bool IsSystemOnly);

internal sealed class ReferenceDataMappingProfile : Profile
{
    public ReferenceDataMappingProfile()
    {
        CreateMap<CauseOfLossCode, CauseOfLossCodeDto>();
        CreateMap<ClaimStatusTransition, ClaimStatusTransitionDto>();
    }
}
