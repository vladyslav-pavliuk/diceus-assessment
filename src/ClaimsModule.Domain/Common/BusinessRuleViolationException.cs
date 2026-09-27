namespace ClaimsModule.Domain.Common;

/// <summary>
/// A domain rule rejected the operation (for example "Self-approval is not permitted.").
/// Mapped to HTTP 422 with the FRS §10.4 body. Errors are keyed by field or condition so that one
/// exception can report several failed conditions at once (BR-ST-03 lists every blocking condition).
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
