using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Reserves;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

/// <summary>Runs inside the unit of work, so the claim number commits or rolls back with the claim (D-10).</summary>
internal sealed class CreateClaimCommandHandler(
    IClaimRepository claims,
    IPolicyRepository policies,
    IClaimNumberGenerator claimNumbers,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<CreateClaimCommand, ClaimCreatedDto>
{
    public async Task<ClaimCreatedDto> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var actor = currentUser.ToActor();
        var now = timeProvider.GetUtcNow();

        // Re-read inside the transaction: the validator only proved it existed a moment ago.
        Policy? policy = null;
        if (request.PolicyId is { } policyId)
        {
            policy = await policies.GetAsync(policyId, cancellationToken)
                ?? throw new BusinessRuleViolationException(ErrorKeys.PolicyId, RequestMessages.PolicyNotFound);
        }

        var claimNumber = await claimNumbers.NextAsync(now.Year, cancellationToken);

        var claim = Claim.Create(
            claimNumber,
            policy,
            new LossEventDetails(
                request.LossDate!.Value,
                request.LossDescription!,
                request.LossLocation,
                request.CauseOfLossCode!,
                request.EstimatedLossAmount,
                request.PoliceReportNumber),
            request.Severity ?? ClaimSeverity.Standard,
            (request.Parties ?? []).Select(party => party.ToPartyDetails()).ToList(),
            (request.RiskObjects ?? []).Select(riskObject => riskObject.ToRiskObjectDetails()).ToList(),
            actor,
            now);

        // The same domain method as a later reserve submission, so the authority and limit rules are identical.
        ReserveSubmittedDto? initialReserve = null;
        if (request.InitialReserve is { } reserve)
        {
            var submitted = claim.SubmitReserveTransaction(
                reserve.Component!.Value,
                ReserveTransactionType.Add,
                reserve.Amount,
                string.IsNullOrWhiteSpace(reserve.ChangeReason) ? InitialReserveInput.DefaultChangeReason : reserve.ChangeReason,
                actor,
                now);

            initialReserve = new ReserveSubmittedDto(
                reserve.Component.Value, mapper.Map<ReserveTransactionDto>(submitted.Transaction), submitted.Warnings);
        }

        claims.Add(claim);

        return new ClaimCreatedDto(
            claim.Id,
            claim.ClaimNumber,
            claim.Status,
            mapper.Map<IReadOnlyList<ValidationIssueDto>>(claim.ValidationIssues),
            initialReserve);
    }
}
