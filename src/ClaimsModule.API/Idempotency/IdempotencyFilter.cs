using System.Security.Cryptography;
using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClaimsModule.API.Idempotency;

/// <summary>
/// FRS §10: "All write operations are idempotent where an Idempotency-Key header is provided" (D-24).
/// A resource filter, so it runs before model binding and can read the raw body, and it wraps result
/// execution, so it can store the exact bytes that were sent. The replayed thing is an HTTP response
/// (status, body, Location), which a MediatR behaviour never sees (REVIEW-PREP).
/// <list type="bullet">
/// <item>New key: run the request. Store a 2xx response; any other outcome frees the key (D-40).</item>
/// <item>Same key, same request, finished: replay the stored response, marked Idempotency-Replayed.</item>
/// <item>Same key, same request, still running: 409.</item>
/// <item>Same key, different method, route or body: 422.</item>
/// </list>
/// Only authenticated POST/PUT/PATCH/DELETE requests take part; the key is scoped to the user.
/// </summary>
internal sealed class IdempotencyFilter(IIdempotencyStore store, ICurrentUser currentUser) : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    /// <summary>The IdempotencyRecords.Key column size (D-24).</summary>
    public const int MaxKeyLength = 200;

    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(key) || !WriteMethods.Contains(http.Request.Method) || currentUser.UserId is not { } userId)
        {
            await next();
            return;
        }

        if (key.Length > MaxKeyLength || string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleViolationException(HeaderName, "Idempotency-Key must be 1 to 200 characters.");
        }

        var request = new IdempotentRequest(
            userId, key, http.Request.Method, http.Request.Path + http.Request.QueryString, await HashRequestAsync(http.Request));

        switch (await store.BeginAsync(request, http.RequestAborted))
        {
            case IdempotencyClaim.Replay replay:
                await WriteReplayAsync(http.Response, replay.Response);
                context.Result = new EmptyResult(); // the stored response is already written
                return;

            case IdempotencyClaim.InProgress:
                throw new ConflictException("A request with this Idempotency-Key is still being processed.");

            case IdempotencyClaim.Mismatch:
                throw new BusinessRuleViolationException(HeaderName, "Idempotency-Key reuse with a different request.");

            case IdempotencyClaim.Started started:
                await ExecuteAndRecordAsync(context, next, started.RecordId);
                return;
        }
    }

    private async Task ExecuteAndRecordAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, Guid recordId)
    {
        var response = context.HttpContext.Response;
        var originalBody = response.Body;
        await using var buffer = new MemoryStream();
        response.Body = buffer;

        ResourceExecutedContext executed;
        try
        {
            executed = await next();
        }
        finally
        {
            // Restore before anything else writes: an exception is rendered by the error middleware
            // on the real response stream.
            response.Body = originalBody;
        }

        // Not recorded: exceptions (rendered later as 4xx/5xx by the middleware) and non-2xx results.
        // Freeing the key lets the client retry, which is what a 409 or a 5xx calls for (D-24, D-40).
        var succeeded = executed.Exception is null && response.StatusCode is >= 200 and < 300;
        if (succeeded)
        {
            var body = Encoding.UTF8.GetString(buffer.ToArray());
            var location = response.Headers.Location.ToString();
            await store.CompleteAsync(
                recordId, new StoredResponse(response.StatusCode, body, string.IsNullOrEmpty(location) ? null : location), CancellationToken.None);
        }
        else
        {
            await store.ReleaseAsync(recordId, CancellationToken.None);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.HttpContext.RequestAborted);
    }

    private static async Task<string> HashRequestAsync(HttpRequest request)
    {
        var bodyHash = request.HasFormContentType ? await HashFormAsync(request) : await HashRawBodyAsync(request);

        var identity = Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n");
        return Convert.ToHexString(SHA256.HashData([.. identity, .. bodyHash]));
    }

    private static async Task<byte[]> HashRawBodyAsync(HttpRequest request)
    {
        // Buffered so model binding can read the body again after hashing.
        request.EnableBuffering();
        request.Body.Position = 0;
        var hash = await SHA256.HashDataAsync(request.Body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return hash;
    }

    /// <summary>
    /// A form is hashed by its content, not its bytes (D-42): a multipart body carries a random boundary, so a client that
    /// retries an upload with a new FormData sends the same form in different bytes. Every field value and every file (field
    /// name, file name, content type, SHA-256 of the bytes) goes in, length-prefixed and in a fixed order. The form is parsed
    /// once here, within the endpoint's form limits; model binding then reuses the parsed form.
    /// </summary>
    private static async Task<byte[]> HashFormAsync(HttpRequest request)
    {
        var cancellationToken = request.HttpContext.RequestAborted;
        var form = await request.ReadFormAsync(cancellationToken);
        var canonical = new StringBuilder();

        foreach (var field in form.OrderBy(field => field.Key, StringComparer.Ordinal))
        {
            foreach (var value in field.Value)
            {
                Append(canonical, "field", field.Key, value);
            }
        }

        foreach (var file in form.Files.OrderBy(file => file.Name, StringComparer.Ordinal).ThenBy(file => file.FileName, StringComparer.Ordinal))
        {
            await using var content = file.OpenReadStream();
            var contentHash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken));
            Append(canonical, "file", file.Name, file.FileName, file.ContentType, contentHash);
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));

        static void Append(StringBuilder builder, params string?[] values)
        {
            foreach (var value in values)
            {
                builder.Append(value?.Length ?? -1).Append(':').Append(value).Append(';');
            }
        }
    }

    private static async Task WriteReplayAsync(HttpResponse response, StoredResponse stored)
    {
        response.StatusCode = stored.StatusCode;
        response.Headers[ReplayedHeaderName] = "true";
        if (stored.Location is not null)
        {
            response.Headers.Location = stored.Location;
        }

        if (stored.Body.Length > 0)
        {
            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync(stored.Body, response.HttpContext.RequestAborted);
        }
    }
}
