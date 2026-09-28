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
/// Sets an absolute <paramref name="NewAmount"/>, turned into a delta and submitted as an Adjust through the same
/// domain method as a POST, so the authority and limit rules are identical (D-04).
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

        // The component's RowVer turns a concurrent change into a 409, so the delta is never based on stale data.
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
