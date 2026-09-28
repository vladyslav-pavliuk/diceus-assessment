using AutoMapper;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.ValidateClaim;

/// <summary>
/// POST /api/claims/{id}/validate (FRS §5.4 "the validate endpoint", D-08). Re-runs every rule that is
/// persisted as an issue: newly failing rules raise issues, passing rules resolve them. It changes
/// data, so it is a command. Returns every issue of the claim.
/// </summary>
public sealed record ValidateClaimCommand(Guid ClaimId) : ICommand<IReadOnlyList<ValidationIssueDto>>;

internal sealed class ValidateClaimCommandHandler(
    IClaimRepository claims,
    IPolicyRepository policies,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<ValidateClaimCommand, IReadOnlyList<ValidationIssueDto>>
{
    public async Task<IReadOnlyList<ValidationIssueDto>> Handle(ValidateClaimCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        Policy? linkedPolicy = null;
        if (claim.PolicyId is { } policyId)
        {
            linkedPolicy = await policies.GetAsync(policyId, cancellationToken)
                ?? throw new InvalidOperationException($"Claim {claim.Id} is linked to policy {policyId}, which does not exist.");
        }

        claim.Revalidate(linkedPolicy, timeProvider.GetUtcNow());
        return mapper.Map<IReadOnlyList<ValidationIssueDto>>(claim.ValidationIssues);
    }
}
