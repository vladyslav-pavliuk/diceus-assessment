using MediatR;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Outermost pipeline behaviour: logs every request's name, outcome and duration as structured
/// properties. The correlation id is already in the logging scope (API middleware).
/// Request payloads are not logged, because they contain personal data (party names, contacts).
/// </summary>
internal sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TimeProvider timeProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = timeProvider.GetTimestamp();

        try
        {
            var response = await next(cancellationToken);

            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds} ms",
                requestName,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);

            return response;
        }
        catch (Exception exception)
        {
            // Warning, not Error: most failures here are expected 4xx outcomes (validation, business
            // rules). The exception middleware logs unexpected ones as errors with the stack trace.
            logger.LogWarning(
                "{RequestName} failed after {ElapsedMilliseconds} ms with {ExceptionType}",
                requestName,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds,
                exception.GetType().Name);

            throw;
        }
    }
}
