using ClaimsModule.API.Errors;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Storage;

/// <summary>
/// Exists only with the local storage fallback, which start-up validation allows only in Development (D-28). Anonymous like a
/// SAS URL: a browser tab cannot send the Bearer header, so the signed token is the credential.
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
                // One answer for every bad token, like Storage's 403 for a bad SAS.
                var download = storage.OpenDownload(token);
                if (download is null)
                {
                    return Results.Json(
                        ErrorResponseFactory.Create(StatusCodes.Status403Forbidden, "The download link is invalid or has expired."),
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
