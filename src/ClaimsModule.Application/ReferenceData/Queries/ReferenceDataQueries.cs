using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.ReferenceData;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.ReferenceData.Queries;

public sealed record ListCauseOfLossCodesQuery(PerilCategory? PerilCategory = null) : IQuery<IReadOnlyList<CauseOfLossCodeDto>>;

internal sealed class ListCauseOfLossCodesQueryValidator : AbstractValidator<ListCauseOfLossCodesQuery>
{
    public ListCauseOfLossCodesQueryValidator()
    {
        RuleFor(query => query.PerilCategory).IsInEnum().WithMessage(RequestMessages.InvalidPerilCategory);
    }
}

internal sealed class ListCauseOfLossCodesQueryHandler(IReferenceDataQueries referenceData)
    : IRequestHandler<ListCauseOfLossCodesQuery, IReadOnlyList<CauseOfLossCodeDto>>
{
    public Task<IReadOnlyList<CauseOfLossCodeDto>> Handle(ListCauseOfLossCodesQuery request, CancellationToken cancellationToken) =>
        referenceData.ListCauseOfLossCodesAsync(request.PerilCategory, cancellationToken);
}

/// <summary>Reads the rows the aggregate enforces, so the UI menu and the rules cannot drift (D-09).</summary>
public sealed record ListClaimStatusesQuery : IQuery<IReadOnlyList<ClaimStatusDto>>;

internal sealed class ListClaimStatusesQueryHandler(IReferenceDataQueries referenceData)
    : IRequestHandler<ListClaimStatusesQuery, IReadOnlyList<ClaimStatusDto>>
{
    public async Task<IReadOnlyList<ClaimStatusDto>> Handle(ListClaimStatusesQuery request, CancellationToken cancellationToken)
    {
        var transitions = await referenceData.ListStatusTransitionsAsync(cancellationToken);

        return Enum.GetValues<ClaimStatus>()
            .Select(status => new ClaimStatusDto(
                status,
                transitions.Where(transition => transition.FromStatus == status).OrderBy(transition => transition.ToStatus).ToList()))
            .ToList();
    }
}
