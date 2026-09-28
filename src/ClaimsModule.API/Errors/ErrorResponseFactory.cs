using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Errors;

/// <summary>
/// The single source of the FRS §10.4 error shape: ProblemDetails with a short "type" code instead of a URI. The correlation
/// id travels in a header, so the 422 body stays exactly as specified.
/// </summary>
internal static class ErrorResponseFactory
{
    public const string ValidationTitle = "One or more validation errors occurred.";

    public static string TypeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "BadRequest",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "NotFound",
        StatusCodes.Status405MethodNotAllowed => "MethodNotAllowed",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status413PayloadTooLarge => "PayloadTooLarge",
        StatusCodes.Status415UnsupportedMediaType => "UnsupportedMediaType",
        StatusCodes.Status422UnprocessableEntity => "ValidationError",
        >= StatusCodes.Status500InternalServerError => "ServerError",
        _ => "Error",
    };

    public static HttpValidationProblemDetails Validation(IDictionary<string, string[]> errors) =>
        new(errors)
        {
            Type = TypeFor(StatusCodes.Status422UnprocessableEntity),
            Title = ValidationTitle,
            Status = StatusCodes.Status422UnprocessableEntity,
        };

    public static ProblemDetails Create(int statusCode, string title, string? detail = null) =>
        new()
        {
            Type = TypeFor(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
        };

    /// <summary>Gives the framework's own ProblemDetails (challenges, unknown routes) the same shape.</summary>
    public static void Normalise(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;

        problem.Status = statusCode;
        problem.Type = TypeFor(statusCode);
        if (statusCode == StatusCodes.Status422UnprocessableEntity)
        {
            problem.Title = ValidationTitle;
        }

        problem.Extensions.Remove("traceId");
    }
}
