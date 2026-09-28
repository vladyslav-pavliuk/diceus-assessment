using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries.GetClaimAudit;

public sealed record GetClaimAuditQuery(Guid ClaimId, int Page = 1, int PageSize = ClaimQueryLimits.DefaultAuditPageSize)
    : IQuery<PagedResult<AuditEntryDto>>;

internal sealed class GetClaimAuditQueryValidator : AbstractValidator<GetClaimAuditQuery>
{
    public GetClaimAuditQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithMessage(RequestMessages.PageMustBePositive);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, ClaimQueryLimits.MaxAuditPageSize).WithMessage(RequestMessages.PageSizeOutOfRange(ClaimQueryLimits.MaxAuditPageSize));
    }
}

internal sealed class GetClaimAuditQueryHandler(IClaimQueries claims) : IRequestHandler<GetClaimAuditQuery, PagedResult<AuditEntryDto>>
{
    public async Task<PagedResult<AuditEntryDto>> Handle(GetClaimAuditQuery request, CancellationToken cancellationToken) =>
        await claims.GetAuditAsync(request.ClaimId, new PageRequest(request.Page, request.PageSize), cancellationToken)
        ?? throw new NotFoundException(nameof(Claim), request.ClaimId);
}
