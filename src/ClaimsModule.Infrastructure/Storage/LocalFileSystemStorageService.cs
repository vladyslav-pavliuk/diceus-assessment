using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Development fallback (BR-D-03). The download token plays the part of a SAS: HMAC-signed with a per-process key, so it cannot
/// be forged and dies with a restart. Streaming bytes through the API is the documented deviation from BR-D-02 (D-28).
/// </summary>
public sealed class LocalFileSystemStorageService : IStorageService
{
    public const string DownloadRoutePrefix = "api/local-files/";

    private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);
    private readonly Uri _publicBaseUrl;
    private readonly TimeProvider _timeProvider;

    public LocalFileSystemStorageService(IOptions<StorageOptions> options, IHostEnvironment environment, TimeProvider timeProvider)
    {
        var settings = options.Value.LocalFileSystem;
        RootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.RootPath)));
        _publicBaseUrl = settings.PublicBaseUrl ?? throw new InvalidOperationException("Storage:LocalFileSystem:PublicBaseUrl is required.");
        _timeProvider = timeProvider;
    }

    public string RootPath { get; }

    public async Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var path = ResolveFullPath(objectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // CreateNew throws for an existing file, which is therefore never overwritten or deleted below.
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81_920, useAsync: true);
        try
        {
            await using (file)
            {
                await content.CopyToAsync(file, cancellationToken);
            }
        }
        catch
        {
            File.Delete(path); // no half-written file
            throw;
        }
    }

    public Task<SignedDownloadUrl> GetDownloadUrlAsync(
        string objectPath, DownloadHeaders headers, TimeSpan validFor, CancellationToken cancellationToken)
    {
        ResolveFullPath(objectPath);
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds((_timeProvider.GetUtcNow() + validFor).ToUnixTimeSeconds());

        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new DownloadToken(objectPath, expiresAt.ToUnixTimeSeconds(), headers.ContentType, headers.FileName, headers.Inline));
        var token = $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(HMACSHA256.HashData(_signingKey, payload))}";

        return Task.FromResult(new SignedDownloadUrl(new Uri(_publicBaseUrl, DownloadRoutePrefix + token), expiresAt));
    }

    public Task DeleteAsync(string objectPath, CancellationToken cancellationToken)
    {
        var path = ResolveFullPath(objectPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Null for every kind of bad token, so a probe learns nothing about which check failed.</summary>
    public LocalFileDownload? OpenDownload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2 || !Base64Url.IsValid(parts[0]) || !Base64Url.IsValid(parts[1]))
        {
            return null;
        }

        var payload = Base64Url.DecodeFromChars(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(Base64Url.DecodeFromChars(parts[1]), HMACSHA256.HashData(_signingKey, payload)))
        {
            return null;
        }

        var grant = JsonSerializer.Deserialize<DownloadToken>(payload)!;
        if (_timeProvider.GetUtcNow() >= DateTimeOffset.FromUnixTimeSeconds(grant.ExpiresAt))
        {
            return null;
        }

        var path = ResolveFullPath(grant.Path);
        return File.Exists(path)
            ? new LocalFileDownload(path, grant.ContentType, ContentDispositions.Format(new DownloadHeaders(grant.ContentType, grant.FileName, grant.Inline)))
            : null;
    }

    /// <summary>Defence in depth against path traversal. Callers pass only DocumentBlobPath values, so a failure is a bug.</summary>
    private string ResolveFullPath(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath)
            || Path.IsPathRooted(objectPath)
            || objectPath.Contains('\\', StringComparison.Ordinal)
            || objectPath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException($"Invalid object path '{objectPath}'.", nameof(objectPath));
        }

        var path = Path.GetFullPath(Path.Combine(RootPath, objectPath));
        if (!path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Object path '{objectPath}' resolves outside the storage root.", nameof(objectPath));
        }

        return path;
    }

    private sealed record DownloadToken(string Path, long ExpiresAt, string ContentType, string FileName, bool Inline);
}

public sealed record LocalFileDownload(string FullPath, string ContentType, string ContentDisposition);
