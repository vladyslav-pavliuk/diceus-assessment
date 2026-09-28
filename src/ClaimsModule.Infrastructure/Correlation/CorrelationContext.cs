using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Correlation;

/// <summary>Set once per request by the middleware, or once per run by a background job.</summary>
public sealed class CorrelationContext : ICorrelationContext
{
    private string? _correlationId;

    public string CorrelationId =>
        _correlationId ?? throw new InvalidOperationException("The correlation id has not been set for this scope.");

    public void Set(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (_correlationId is not null)
        {
            throw new InvalidOperationException("The correlation id is already set for this scope.");
        }

        _correlationId = correlationId;
    }
}
