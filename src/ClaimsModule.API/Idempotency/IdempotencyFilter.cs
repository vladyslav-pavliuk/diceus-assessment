using System.Security.Cryptography;
using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClaimsModule.API.Idempotency;

/// <summary>
/// A resource filter rather than a MediatR behaviour, because what it replays is the HTTP response (status, body,
/// Location). Only 2xx responses are stored; any other outcome frees the key. A request still running gets 409, and
/// the same key with a different request gets 422 (D-24, D-40).
/// </summary>
internal sealed class IdempotencyFilter(IIdempotencyStore store, ICurrentUser currentUser) : IAsyncResourceFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotency-Replayed";

    /// <summary>The Key column size.</summary>
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
            // The error middleware renders exceptions on the real response stream.
            response.Body = originalBody;
        }

        // Exceptions and non-2xx results free the key, so the client can retry (D-40).
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
    /// Hashed by content, not bytes, because a retried upload has a new multipart boundary (D-42). Values are length-prefixed
    /// and ordered, so the hash is canonical.
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
