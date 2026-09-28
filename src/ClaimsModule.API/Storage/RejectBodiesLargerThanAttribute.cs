using ClaimsModule.API.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClaimsModule.API.Storage;

/// <summary>
/// Without it, Kestrel's 413 is caught by form binding and comes back as a 422 with framework wording (D-42). Runs before
/// the Idempotency-Key filter hashes the form. Pair it with [RequestSizeLimit] of the same value.
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
