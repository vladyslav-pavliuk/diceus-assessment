using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RemoveParty;

public sealed record RemovePartyCommand(Guid ClaimId, Guid PartyId) : ICommand;

internal sealed class RemovePartyCommandHandler(IClaimRepository claims, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<RemovePartyCommand>
{
    public async Task Handle(RemovePartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.RemoveParty(request.PartyId, currentUser.ToActor(), timeProvider.GetUtcNow());
    }
}
