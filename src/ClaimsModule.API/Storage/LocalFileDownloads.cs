using ClaimsModule.API.Errors;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Storage;

/// <summary>
/// <c>GET /api/local-files/{token}</c>: the development-only download endpoint of the local storage fallback (FRS §13, D-28).
/// Mapped only when Storage:Provider is LocalFileSystem, which start-up validation allows only in Development, so it does not
/// exist in any deployed API. Anonymous like a SAS URL: a browser tab opened by the SPA cannot send the Bearer header, so the
/// signed, expiring token is the credential. Infrastructure, not a business read, so it does not go through MediatR (the same
/// reasoning as token issuance, D-08).
/// </summary>
internal static class LocalFileDownloads
{
    public static void MapLocalFileDownloads(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<StorageOptions>>().Value.Provider != StorageProvider.LocalFileSystem)
        {
            return;
        }

        app.MapGet($"/{LocalFileSystemStorageService.DownloadRoutePrefix}{{token}}", (string token, LocalFileSystemStorageService storage, HttpResponse response) =>
            {
                // Forged, edited, expired or dangling: one answer for all, like Storage's 403 for a bad SAS.
                var download = storage.OpenDownload(token);
                if (download is null)
                {
                    return Results.Json(
                        ApiProblems.Create(StatusCodes.Status403Forbidden, "The download link is invalid or has expired."),
                        statusCode: StatusCodes.Status403Forbidden,
                        contentType: "application/problem+json");
                }

                response.Headers.ContentDisposition = download.ContentDisposition;
                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers.CacheControl = "private, no-store";
                return Results.File(download.FullPath, download.ContentType, enableRangeProcessing: true);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
