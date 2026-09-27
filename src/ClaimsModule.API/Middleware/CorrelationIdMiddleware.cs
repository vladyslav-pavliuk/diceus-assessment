using ClaimsModule.Infrastructure.Correlation;

namespace ClaimsModule.API.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the request, or creates one when it is absent or not a GUID, then:
/// stores it in the scoped <see cref="CorrelationContext"/> (stamped on audit rows, FRS §14.2),
/// echoes it in the response header, and adds it to the logging scope of the whole request.
/// </summary>
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

    // ClaimAuditLog.CorrelationId is a GUID (FRS §9.8), so only a GUID in the standard 36-character
    // form is accepted from the client (D-39 Q1). Anything else is replaced rather than rejected: a
    // tracing header should never fail a business request, and the echoed id still lets the client correlate.
    private static bool IsAcceptable(string value) => Guid.TryParseExact(value, "D", out _);
}
