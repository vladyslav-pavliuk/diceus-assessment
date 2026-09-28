using AutoMapper;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Inputs;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AddRiskObject;

/// <summary>
/// POST /api/claims/{id}/risk-objects (D-40, decided by Vlad 2026-09-28): adds a damaged asset after
/// FNOL, so the "no risk objects" warning (FRS §5.4) can be cleared later, not only at intake. The
/// first risk object becomes primary, and one marked primary takes over (D-33). Audit: RISK_OBJECT_ADDED.
/// </summary>
public sealed record AddRiskObjectCommand(
    Guid ClaimId,
    AssetType? AssetType,
    string? AssetDescription,
    string? DamageDescription,
    string? AssetReference,
    bool IsPrimary) : ICommand<RiskObjectDto>, IRiskObjectFields;

internal sealed class AddRiskObjectCommandValidator : AbstractValidator<AddRiskObjectCommand>
{
    public AddRiskObjectCommandValidator() => Include(new RiskObjectFieldsValidator<AddRiskObjectCommand>());
}

internal sealed class AddRiskObjectCommandHandler(IClaimRepository claims, ICurrentUser currentUser, TimeProvider timeProvider, IMapper mapper)
    : IRequestHandler<AddRiskObjectCommand, RiskObjectDto>
{
    public async Task<RiskObjectDto> Handle(AddRiskObjectCommand request, CancellationToken cancellationToken)
    {
        var claim = await claims.GetAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var riskObject = claim.AddRiskObject(request.ToRiskObjectDetails(), currentUser.ToActor(), timeProvider.GetUtcNow());
        return mapper.Map<RiskObjectDto>(riskObject);
    }
}
