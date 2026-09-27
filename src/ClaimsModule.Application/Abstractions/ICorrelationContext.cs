namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// The correlation id of the current unit of work. For HTTP requests it comes from the
/// X-Correlation-Id header, or is generated when the header is absent or invalid. Background jobs
/// use their own id. It is stamped on every audit row written in that scope (FRS §14.2).
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}
