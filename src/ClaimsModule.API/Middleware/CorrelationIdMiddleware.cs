using ClaimsModule.Infrastructure.Correlation;

namespace ClaimsModule.API.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the request, or creates one when it is absent or invalid, then:
/// stores it in the scoped <see cref="CorrelationContext"/> (stamped on audit rows, FRS §14.2),
/// echoes it in the response header, and adds it to the logging scope of the whole request.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlationContext)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (!IsAcceptable(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        correlationContext.Set(correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    // The value is written to logs, audit rows and a response header, so only a short, plain token
    // is accepted from the client. Anything else is replaced rather than rejected.
    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
