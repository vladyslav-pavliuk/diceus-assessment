using ClaimsModule.Infrastructure.Correlation;

namespace ClaimsModule.API.Middleware;

/// <summary>Reads or creates X-Correlation-Id, echoes it, and puts it on the audit rows and the logging scope.</summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

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

    // The audit column is a GUID (D-39). Anything else is replaced rather than rejected: a tracing header should
    // never fail a business request.
    private static bool IsAcceptable(string value) => Guid.TryParseExact(value, "D", out _);
}
