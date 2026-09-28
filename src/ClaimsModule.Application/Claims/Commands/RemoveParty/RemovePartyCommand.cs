using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.RemoveParty;

/// <summary>
/// DELETE /api/claims/{id}/parties/{partyId} (FRS §10.1): a soft remove, IsActive = false (D-27). The
/// last active Claimant cannot be removed (422, PTY-01). Audit: PARTY_REMOVED.
/// </summary>
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
