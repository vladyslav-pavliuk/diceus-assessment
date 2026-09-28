namespace ClaimsModule.Domain.Common;

/// <summary>
/// A domain rule rejected the operation (HTTP 422). Errors are keyed by field or condition, so one
/// exception reports every failed condition (BR-ST-03).
/// </summary>
public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string key, string message)
        : this(new Dictionary<string, string[]> { [key] = [message] })
    {
    }

    public BusinessRuleViolationException(IReadOnlyDictionary<string, string[]> errors)
        : base(string.Join(" ", errors.Values.SelectMany(messages => messages)))
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one error is required.", nameof(errors));
        }

        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
