using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Downloads are read-only SAS URLs, so the API never proxies bytes (BR-D-02). With managed identity the SAS is a
/// user-delegation SAS whose key is cached, so listing documents costs no Storage round trip per document.
/// Uploads use If-None-Match: * and never overwrite.
/// </summary>
public sealed class AzureBlobStorageService : IStorageService
{
    /// <summary>Tolerates a local clock running slightly behind Storage's.</summary>
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

    /// <summary>For Azurite, which starts empty; in Azure the infrastructure template creates the container.</summary>
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
