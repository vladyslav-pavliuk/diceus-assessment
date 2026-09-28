using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.LinkPolicy;

public sealed record LinkPolicyCommand(Guid ClaimId, Guid? PolicyId) : ICommand;

internal sealed class LinkPolicyCommandValidator : AbstractValidator<LinkPolicyCommand>
{
    public LinkPolicyCommandValidator(IPolicyQueries policies)
    {
        RuleFor(command => command.PolicyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(RequestMessages.PolicyRequired)
            .MustAsync(async (policyId, cancellationToken) => await policies.GetAsync(policyId!.Value, cancellationToken) is not null)
            .WithMessage(RequestMessages.PolicyNotFound);
    }
}

internal sealed class LinkPolicyCommandHandler(
    IClaimRepository claims,
    IPolicyRepository policies,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<LinkPolicyCommand>
{
    public async Task Handle(LinkPolicyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var policy = await policies.GetAsync(request.PolicyId!.Value, cancellationToken)
            ?? throw new BusinessRuleViolationException(ErrorKeys.PolicyId, RequestMessages.PolicyNotFound);

        claim.LinkPolicy(policy, currentUser.ToActor(), timeProvider.GetUtcNow());
    }
}
