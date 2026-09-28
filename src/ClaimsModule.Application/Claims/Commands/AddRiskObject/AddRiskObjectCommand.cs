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

/// <summary>Lets the "no risk objects" warning be cleared after intake (D-40).</summary>
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
