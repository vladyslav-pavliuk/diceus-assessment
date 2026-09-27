namespace ClaimsModule.Domain.Common;

/// <summary>
/// Collects every failed rule of one operation, so the caller gets all of them in one 422
/// (BR-ST-03: "a list of blocking conditions") instead of fixing them one at a time.
/// </summary>
internal sealed class RuleViolations
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool Any => _errors.Count > 0;

    public void Add(string key, string message)
    {
        if (!_errors.TryGetValue(key, out var messages))
        {
            messages = [];
            _errors.Add(key, messages);
        }

        messages.Add(message);
    }

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new BusinessRuleViolationException(
                _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
        }
    }
}
