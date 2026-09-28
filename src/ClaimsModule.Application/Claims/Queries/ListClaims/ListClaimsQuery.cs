using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.ListClaims;

/// <summary>
/// GET /api/claims (FRS §10.1, D-29): the dashboard table. <see cref="Statuses"/> may repeat
/// (?status=Open&amp;status=Draft) for the multi-select filter. <see cref="DateFrom"/> and
/// <see cref="DateTo"/> filter the loss date (FRS §11.1), inclusive. Sorted by reported date, newest first.
/// </summary>
public sealed record ListClaimsQuery(
    IReadOnlyList<ClaimStatus>? Statuses = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? AssignedHandlerId = null,
    string? CauseOfLossCode = null,
    Guid? PolicyId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = ClaimQueryLimits.DefaultPageSize) : IQuery<PagedResult<ClaimSummaryDto>>;

internal sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithMessage(RequestMessages.PageMustBePositive);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, ClaimQueryLimits.MaxPageSize).WithMessage(RequestMessages.PageSizeOutOfRange(ClaimQueryLimits.MaxPageSize));

        RuleForEach(query => query.Statuses).IsInEnum().WithMessage(RequestMessages.InvalidClaimStatus);

        RuleFor(query => query.DateTo)
            .GreaterThanOrEqualTo(query => query.DateFrom).When(query => query.DateFrom is not null && query.DateTo is not null)
            .WithMessage(RequestMessages.LossDateRangeInvalid);

        RuleFor(query => query.Search)
            .MaximumLength(ClaimQueryLimits.MaxSearchLength).WithMessage(RequestMessages.TooLong("Search", ClaimQueryLimits.MaxSearchLength));
    }
}

internal sealed class ListClaimsQueryHandler(IClaimQueries claims) : IRequestHandler<ListClaimsQuery, PagedResult<ClaimSummaryDto>>
{
    public Task<PagedResult<ClaimSummaryDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        var filter = new ClaimListFilter(
            request.Statuses?.Distinct().ToList() ?? [],
            request.DateFrom,
            request.DateTo,
            request.AssignedHandlerId,
            string.IsNullOrWhiteSpace(request.CauseOfLossCode) ? null : request.CauseOfLossCode.Trim(),
            request.PolicyId,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim());

        return claims.ListAsync(filter, new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
