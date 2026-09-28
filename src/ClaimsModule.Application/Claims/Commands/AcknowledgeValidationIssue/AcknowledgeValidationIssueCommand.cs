using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AcknowledgeValidationIssue;

/// <summary>Accepts a Warning with a note (D-07). Acknowledging BR-C-02 unblocks Draft → Open (D-19).</summary>
public sealed record AcknowledgeValidationIssueCommand(Guid ClaimId, Guid IssueId, string? Note) : ICommand<ValidationIssueDto>;

internal sealed class AcknowledgeValidationIssueCommandValidator : AbstractValidator<AcknowledgeValidationIssueCommand>
{
    public AcknowledgeValidationIssueCommandValidator()
    {
        RuleFor(command => command.Note)
            .NotEmpty().WithMessage(DomainMessages.AcknowledgementNoteRequired)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Note", FieldLengths.Reason));
    }
}

internal sealed class AcknowledgeValidationIssueCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<AcknowledgeValidationIssueCommand, ValidationIssueDto>
{
    public async Task<ValidationIssueDto> Handle(AcknowledgeValidationIssueCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        claim.AcknowledgeValidationIssue(request.IssueId, request.Note, currentUser.ToActor(), timeProvider.GetUtcNow());
        return mapper.Map<ValidationIssueDto>(claim.ValidationIssues.Single(issue => issue.Id == request.IssueId));
    }
}
