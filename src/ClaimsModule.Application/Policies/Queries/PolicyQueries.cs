using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Policies.Queries;

/// <summary>Backs a typeahead, so the term is required and results are capped (D-40).</summary>
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
