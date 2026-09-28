using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.UpdateClaimDetails;

/// <summary>A null field is left unchanged; an empty <see cref="Notes"/> clears the notes (D-40).</summary>
public sealed record UpdateClaimDetailsCommand(Guid ClaimId, string? Notes, ClaimSeverity? Severity) : ICommand;

internal sealed class UpdateClaimDetailsCommandValidator : AbstractValidator<UpdateClaimDetailsCommand>
{
    public UpdateClaimDetailsCommandValidator()
    {
        RuleFor(command => command.Severity).IsInEnum().WithMessage(DomainMessages.InvalidClaimSeverity);
    }
}

internal sealed class UpdateClaimDetailsCommandHandler(IClaimRepository claims, ICurrentUser currentUser)
    : IRequestHandler<UpdateClaimDetailsCommand>
{
    public async Task Handle(UpdateClaimDetailsCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var actor = currentUser.ToActor();
        if (request.Notes is not null)
        {
            claim.UpdateNotes(request.Notes, actor);
        }

        if (request.Severity is { } severity)
        {
            claim.ChangeSeverity(severity, actor);
        }
    }
}
