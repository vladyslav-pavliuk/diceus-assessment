using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AddParty;

/// <summary>Adding an active Claimant resolves the BR-C-03 issue in the same transaction (D-07).</summary>
public sealed record AddPartyCommand(
    Guid ClaimId,
    PartyRole? Role,
    PartyType? Type,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string? Notes) : ICommand<ClaimPartyDto>, IPartyFields;

internal sealed class AddPartyCommandValidator : AbstractValidator<AddPartyCommand>
{
    public AddPartyCommandValidator() => Include(new PartyFieldsValidator<AddPartyCommand>());
}

internal sealed class AddPartyCommandHandler(IClaimRepository claims, ICurrentUser currentUser, TimeProvider timeProvider, IMapper mapper)
    : IRequestHandler<AddPartyCommand, ClaimPartyDto>
{
    public async Task<ClaimPartyDto> Handle(AddPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var party = claim.AddParty(request.ToPartyDetails(), currentUser.ToActor(), timeProvider.GetUtcNow());
        return mapper.Map<ClaimPartyDto>(party);
    }
}
