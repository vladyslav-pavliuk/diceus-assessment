using AutoMapper;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.ReferenceData;

/// <summary>GET /api/reference/cause-of-loss-codes (FRS §10.3, §9.9).</summary>
public sealed record CauseOfLossCodeDto(string Code, string Name, PerilCategory PerilCategory, int SortOrder);

/// <summary>
/// GET /api/reference/claim-statuses (FRS §10.3): each status with the transitions out of it, straight
/// from the ClaimStatusTransitions table the domain enforces (D-09).
/// </summary>
public sealed record ClaimStatusDto(ClaimStatus Status, IReadOnlyList<ClaimStatusTransitionDto> Transitions);

/// <summary>
/// One allowed move. <see cref="IsSystemOnly"/> rows (Reopened → Open) are applied automatically and
/// cannot be requested, so a status menu leaves them out.
/// </summary>
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
