namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// Thrown by the ValidationBehavior when FluentValidation rejects a request.
/// Mapped to HTTP 422 with the FRS §10.4 body; <see cref="Errors"/> becomes the "errors" object.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
