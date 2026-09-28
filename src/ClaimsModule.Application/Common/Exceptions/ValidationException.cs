namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>HTTP 422; <see cref="Errors"/> becomes the "errors" object of the FRS §10.4 body.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
