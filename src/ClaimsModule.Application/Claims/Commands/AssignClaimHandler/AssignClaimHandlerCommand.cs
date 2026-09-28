using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AssignClaimHandler;

/// <summary>
/// PUT /api/claims/{id}/assignee (D-08, D-18): supervisors and managers (re)assign the handler. The
/// assignee must be an active user of the caller's organisation. Audit: HANDLER_ASSIGNED.
/// </summary>
public sealed record AssignClaimHandlerCommand(Guid ClaimId, Guid? UserId) : ICommand;

internal sealed class AssignClaimHandlerCommandValidator : AbstractValidator<AssignClaimHandlerCommand>
{
    public AssignClaimHandlerCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty().WithMessage(RequestMessages.UserRequired);
    }
}

internal sealed class AssignClaimHandlerCommandHandler(IClaimRepository claims, IUserRepository users, ICurrentUser currentUser)
    : IRequestHandler<AssignClaimHandlerCommand>
{
    public async Task Handle(AssignClaimHandlerCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        // Tenant-scoped: a user of another organisation is "not found" (D-18).
        var assignee = await users.GetInOrganisationAsync(request.UserId!.Value, cancellationToken)
            ?? throw new BusinessRuleViolationException(ErrorKeys.AssignedHandlerId, RequestMessages.UserNotFound);

        claim.AssignHandler(assignee, currentUser.ToActor());
    }
}
