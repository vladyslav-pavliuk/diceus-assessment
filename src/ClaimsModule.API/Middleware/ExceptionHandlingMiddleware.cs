using ClaimsModule.API.Errors;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.API.Middleware;

/// <summary>
/// The single place where exceptions become HTTP responses (CLAUDE.md rule 12):
/// ValidationException → 422, BusinessRuleViolationException → 422, NotFoundException → 404,
/// ForbiddenAccessException → 403, ConflictException and DbUpdateConcurrencyException → 409, anything else → 500.
/// Controllers and handlers never build error responses themselves.
/// </summary>
internal sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is nobody to answer.
            logger.LogInformation("Request was cancelled by the client");
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var problem = ToProblem(exception);
            Log(exception, problem.Status!.Value);

            context.Response.StatusCode = problem.Status.Value;
            await context.Response.WriteAsJsonAsync(
                problem,
                problem.GetType(),
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }

    private ProblemDetails ToProblem(Exception exception) => exception switch
    {
        ValidationException validation => ErrorResponseFactory.Validation(ToDictionary(validation.Errors)),

        BusinessRuleViolationException violation => ErrorResponseFactory.Validation(ToDictionary(violation.Errors)),

        NotFoundException notFound => ErrorResponseFactory.Create(
            StatusCodes.Status404NotFound, "The requested resource was not found.", notFound.Message),

        ForbiddenAccessException forbidden => ErrorResponseFactory.Create(
            StatusCodes.Status403Forbidden, "You do not have permission to perform this action.", forbidden.Message),

        ConflictException conflict => ErrorResponseFactory.Create(
            StatusCodes.Status409Conflict, "The request conflicts with the current state of the resource.", conflict.Message),

        DbUpdateConcurrencyException => ErrorResponseFactory.Create(
            StatusCodes.Status409Conflict,
            "The resource was changed by another request. Reload it and try again."),

        BadHttpRequestException badRequest => ErrorResponseFactory.Create(
            badRequest.StatusCode, "The request could not be processed.", badRequest.Message),

        _ => ErrorResponseFactory.Create(
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred.",
            environment.IsDevelopment() ? exception.ToString() : null),
    };

    private void Log(Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            // Expected outcomes: no stack trace, the exception type is enough to follow the request.
            logger.LogInformation("Request failed with {StatusCode} ({ExceptionType})", statusCode, exception.GetType().Name);
        }
    }

    private static Dictionary<string, string[]> ToDictionary(IReadOnlyDictionary<string, string[]> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}
