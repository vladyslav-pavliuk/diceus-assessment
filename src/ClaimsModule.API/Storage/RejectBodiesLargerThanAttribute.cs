using ClaimsModule.API.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClaimsModule.API.Storage;

/// <summary>
/// Answers 413 before anything reads the body when its declared Content-Length is over the endpoint's limit (DOC-06, D-42).
/// Without it, Kestrel's 413 is caught by MVC form binding and comes back as a 422 with an empty key and framework wording.
/// Runs first among the resource filters, before the Idempotency-Key filter hashes the form. A chunked body without
/// Content-Length still meets Kestrel's limit, through binding. Pair it with [RequestSizeLimit] of the same value.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RejectBodiesLargerThanAttribute(long maxBytes) : Attribute, IResourceFilter, IOrderedFilter
{
    public int Order => int.MinValue;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.Request.ContentLength > maxBytes)
        {
            context.Result = new ObjectResult(ErrorResponseFactory.Create(
                StatusCodes.Status413PayloadTooLarge, "The request body is too large.", $"The limit is {maxBytes} bytes; a document may be at most 50 MB."))
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge,
                ContentTypes = { "application/problem+json" },
            };
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
