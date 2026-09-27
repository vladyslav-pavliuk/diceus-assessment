using FluentValidation;
using MediatR;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request before the handler, and
/// short-circuits with a <see cref="ValidationException"/> (HTTP 422, FRS §10.4) when any rule
/// fails. Validation therefore lives in the pipeline, not in controllers (CLAUDE.md rule 2), and
/// runs before a command opens its transaction.
/// </summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        // Sequential on purpose: validators may run reference-data lookups on the request's scoped
        // DbContext, and a DbContext does not support concurrent operations.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            var errors = failures
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                    StringComparer.Ordinal);

            throw new ValidationException(errors);
        }

        return await next(cancellationToken);
    }
}
