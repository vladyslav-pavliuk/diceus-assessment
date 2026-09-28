namespace ClaimsModule.Application.Abstractions;

/// <summary>Always a GUID in "D" format (D-39). Stamped on every audit row of the scope.</summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}
