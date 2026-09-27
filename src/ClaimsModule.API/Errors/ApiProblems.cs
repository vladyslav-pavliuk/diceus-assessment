using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Errors;

/// <summary>
/// One source for the error body shape (FRS §10.4). Every error response is a ProblemDetails
/// object with a short "type" code instead of a URI. The 422 body is exactly
/// { type: "ValidationError", title, status, errors: { field: [messages] } }.
/// The correlation id is returned in the X-Correlation-Id header, not in the body, so the 422 body
/// stays exactly as specified.
/// </summary>
internal static class ApiProblems
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

    /// <summary>
    /// Applied to ProblemDetails that the framework creates itself (401/403 challenges, 404 for
    /// unknown routes, [ApiController] client errors), so they match the ones we create.
    /// </summary>
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
