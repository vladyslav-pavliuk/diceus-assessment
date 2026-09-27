using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Correlation;

/// <summary>
/// Scoped holder for the correlation id. The API middleware sets it once per request; a
/// background job sets it once per execution (Phase 4).
/// </summary>
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
