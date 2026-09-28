using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Documents in Azure Blob Storage (FRS §13, BR-D-01, BR-D-02), container <c>claim-documents</c>. Downloads are read-only SAS
/// URLs that go straight from the browser to Storage; the API never proxies the bytes.
/// <list type="bullet">
/// <item><b>Account key</b> (a connection string, e.g. Azurite): the SAS is signed locally with the key.</item>
/// <item><b>Managed identity</b> (DefaultAzureCredential in Azure): a <b>user-delegation SAS</b>, signed with a user delegation
/// key obtained through Entra ID, so no account key is ever configured. The key is cached and renewed before it would
/// expire under a SAS, so listing documents costs no Storage round trip per document.</item>
/// </list>
/// Uploads never overwrite: the write is conditional on the blob not existing (If-None-Match: *).
/// </summary>
public sealed class AzureBlobStorageService : IStorageService
{
    /// <summary>A SAS starts slightly in the past, so a clock that runs a little behind Storage's does not reject a new URL.</summary>
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromDays(1);

    private readonly BlobServiceClient _service;
    private readonly BlobContainerClient _container;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _containerExists;
    private UserDelegationKey? _delegationKey;

    public AzureBlobStorageService(BlobServiceClient service, string containerName, TimeProvider timeProvider)
    {
        _service = service;
        _container = service.GetBlobContainerClient(containerName);
        _timeProvider = timeProvider;
    }

    public async Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

        await _container.GetBlobClient(objectPath).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            },
            cancellationToken);
    }

    public async Task<SignedDownloadUrl> GetDownloadUrlAsync(
        string objectPath, DownloadHeaders headers, TimeSpan validFor, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(objectPath);
        var now = _timeProvider.GetUtcNow();

        // SAS times have whole-second precision; the returned expiry is the one in the URL.
        var expiresAt = TruncateToSeconds(now + validFor);
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, expiresAt)
        {
            BlobContainerName = _container.Name,
            BlobName = objectPath,
            Resource = "b",
            StartsOn = TruncateToSeconds(now - ClockSkewAllowance),
            Protocol = blob.Uri.Scheme == Uri.UriSchemeHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp,

            // Response headers are part of the signature: the browser gets exactly these, whatever it asks for.
            ContentType = headers.ContentType,
            ContentDisposition = ContentDispositions.Format(headers),
        };

        if (blob.CanGenerateSasUri)
        {
            return new SignedDownloadUrl(blob.GenerateSasUri(sas), expiresAt);
        }

        var key = await GetDelegationKeyAsync(now, expiresAt, cancellationToken);
        var uri = new BlobUriBuilder(blob.Uri) { Sas = sas.ToSasQueryParameters(key, _service.AccountName) }.ToUri();
        return new SignedDownloadUrl(uri, expiresAt);
    }

    public async Task DeleteAsync(string objectPath, CancellationToken cancellationToken) =>
        await _container.GetBlobClient(objectPath).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

    /// <summary>Created on first use (Azurite starts empty); in Azure the infrastructure template creates it, and this is a no-op.</summary>
    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerExists)
        {
            return;
        }

        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        _containerExists = true;
    }

    private async Task<UserDelegationKey> GetDelegationKeyAsync(DateTimeOffset now, DateTimeOffset sasExpiresAt, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // A SAS is only valid while its delegation key is.
            if (_delegationKey is null || _delegationKey.SignedExpiresOn < sasExpiresAt)
            {
                _delegationKey = (await _service.GetUserDelegationKeyAsync(now - ClockSkewAllowance, now + DelegationKeyLifetime, cancellationToken)).Value;
            }

            return _delegationKey;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset);
}
