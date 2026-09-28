using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AdjustReserve;

/// <summary>
/// PUT /api/claims/{id}/reserves/{componentId} (brief §3.3.3 "Adjust reserve amount", D-04): sets a
/// component to an absolute <paramref name="NewAmount"/>. The handler turns it into the delta
/// NewAmount − CurrentAmount and submits an Adjust transaction through the same domain method as POST,
/// so authority, the balance rule and the $10M check are identical. A zero delta is rejected (D-04).
/// </summary>
public sealed record AdjustReserveCommand(Guid ClaimId, Guid ReserveComponentId, decimal? NewAmount, string? ChangeReason)
    : ICommand<ReserveSubmittedDto>;

internal sealed class AdjustReserveCommandValidator : AbstractValidator<AdjustReserveCommand>
{
    public AdjustReserveCommandValidator()
    {
        RuleFor(command => command.NewAmount)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(RequestMessages.NewAmountRequired)
            .Must(amount => Amounts.HasValidScale(amount!.Value)).WithMessage(DomainMessages.TooManyDecimalPlaces("New amount"));

        RuleFor(command => command.ChangeReason)
            .NotEmpty().WithMessage(DomainMessages.ChangeReasonRequired)
            .MaximumLength(FieldLengths.Reason).WithMessage(RequestMessages.TooLong("Change reason", FieldLengths.Reason));
    }
}

internal sealed class AdjustReserveCommandHandler(
    IClaimRepository claims,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<AdjustReserveCommand, ReserveSubmittedDto>
{
    public async Task<ReserveSubmittedDto> Handle(AdjustReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var component = claim.ReserveComponents.SingleOrDefault(candidate => candidate.Id == request.ReserveComponentId)
            ?? throw new NotFoundException("Reserve component", request.ReserveComponentId);

        // CurrentAmount comes from the aggregate loaded in this unit of work; the component's RowVer makes the
        // commit fail (409) if another change took effect in between, so the delta is never computed on stale data.
        var delta = request.NewAmount!.Value - component.CurrentAmount;

        var submitted = claim.SubmitReserveTransaction(
            component.Component,
            ReserveTransactionType.Adjust,
            delta,
            request.ChangeReason,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        return new ReserveSubmittedDto(component.Component, mapper.Map<ReserveTransactionDto>(submitted.Transaction), submitted.Warnings);
    }
}
