using AutoMapper;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.Application.Claims;

/// <summary>Also used with ProjectTo, so every member expression must translate to SQL.</summary>
internal sealed class ClaimMappingProfile : Profile
{
    public ClaimMappingProfile()
    {
        // ClaimParty.DisplayName is a C# property EF cannot translate, so the projection spells it out.
        CreateMap<ClaimParty, ClaimPartyDto>()
            .ForCtorParam(
                nameof(ClaimPartyDto.DisplayName),
                options => options.MapFrom(party => party.PartyType == PartyType.Company ? party.CompanyName : party.FirstName + " " + party.LastName));

        CreateMap<ClaimRiskObject, RiskObjectDto>();
        CreateMap<ClaimValidationIssue, ValidationIssueDto>();
        CreateMap<ClaimDocument, ClaimDocumentDto>();
        CreateMap<ReserveTransaction, ReserveTransactionDto>();
    }
}
