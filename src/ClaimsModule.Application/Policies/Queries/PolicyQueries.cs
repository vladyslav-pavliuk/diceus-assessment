using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Policies.Queries;

/// <summary>
/// GET /api/policies/search?q= (FRS §10.3, §11.2 typeahead): policies whose number or client name
/// contains the term. The term is required and results are capped, because this backs a typeahead (D-40).
/// Named List… because FRS §15.3 allows only Get…/List… query names.
/// </summary>
public sealed record ListPoliciesQuery(string? Q) : IQuery<IReadOnlyList<PolicyDto>>
{
    public const int MaxResults = 20;
    public const int MaxTermLength = 100;
}

internal sealed class ListPoliciesQueryValidator : AbstractValidator<ListPoliciesQuery>
{
    public ListPoliciesQueryValidator()
    {
        RuleFor(query => query.Q)
            .NotEmpty().WithMessage(RequestMessages.SearchTermRequired)
            .MaximumLength(ListPoliciesQuery.MaxTermLength).WithMessage(RequestMessages.TooLong("Search term", ListPoliciesQuery.MaxTermLength));
    }
}

internal sealed class ListPoliciesQueryHandler(IPolicyQueries policies) : IRequestHandler<ListPoliciesQuery, IReadOnlyList<PolicyDto>>
{
    public Task<IReadOnlyList<PolicyDto>> Handle(ListPoliciesQuery request, CancellationToken cancellationToken) =>
        policies.SearchAsync(request.Q!.Trim(), ListPoliciesQuery.MaxResults, cancellationToken);
}

/// <summary>GET /api/policies/{id}/coverage (brief §3.3.2, D-08): the coverage types shown during FNOL.</summary>
public sealed record GetPolicyCoverageQuery(Guid PolicyId) : IQuery<PolicyCoverageDto>;

internal sealed class GetPolicyCoverageQueryHandler(IPolicyQueries policies) : IRequestHandler<GetPolicyCoverageQuery, PolicyCoverageDto>
{
    public async Task<PolicyCoverageDto> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        var policy = await policies.GetAsync(request.PolicyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Policy), request.PolicyId);

        return new PolicyCoverageDto(policy.Id, policy.PolicyNumber, policy.CoverageTypes);
    }
}
