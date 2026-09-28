using System.Net;
using System.Web;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Infrastructure.Storage;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.IntegrationTests.Documents;

/// <summary>
/// The Azure implementation against Azurite, the Azure Storage emulator (BR-D-01, BR-D-02, DOC-01, DOC-02). Azurite signs and
/// checks SAS tokens like Storage does, so the URL is exercised with a plain HttpClient, the way a browser tab uses it: the
/// bytes come from Storage, never from the API. The user-delegation SAS path (managed identity) needs Entra ID and is
/// verified against the real account in Phase 7.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AzureBlobStorageServiceTests(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly DownloadHeaders PdfHeaders = new("application/pdf", "Police Report.pdf", Inline: true);

    private readonly HttpClient _browser = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly string _container = $"claim-documents-{Guid.NewGuid():N}"[..40];
    private BlobServiceClient _blobs = null!;
    private AzureBlobStorageService _storage = null!;

    public async Task InitializeAsync()
    {
        _blobs = new BlobServiceClient(await fixture.AzuriteConnectionStringAsync());
        _storage = new AzureBlobStorageService(_blobs, _container, _clock);
    }

    public Task DisposeAsync()
    {
        _browser.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BR_D_01_Blob_is_stored_under_org_and_claim_prefix_with_its_content_type()
    {
        var path = NewPath("%2e%2e%2f..／secret.pdf"); // encoded and fullwidth traversal: text in the last segment only

        await _storage.UploadAsync(path.Value, new MemoryStream(ClaimsApi.PdfBytes()), "application/pdf", CancellationToken.None);

        var container = _blobs.GetBlobContainerClient(_container);
        var blob = container.GetBlobs(BlobTraits.None, BlobStates.None, $"{path.OrganisationId}/{path.ClaimId}/", CancellationToken.None).ShouldHaveSingleItem();
        blob.Name.ShouldBe($"{path.OrganisationId}/{path.ClaimId}/{path.DocumentId}_secret.pdf");
        blob.Properties.ContentType.ShouldBe("application/pdf");
        container.GetBlobs(BlobTraits.None, BlobStates.None, null, CancellationToken.None).Count().ShouldBe(1); // nothing written outside the claim's prefix
    }

    [Fact]
    public async Task BR_D_02_Sas_url_is_read_only_expires_in_one_hour_and_serves_the_bytes_directly()
    {
        var path = await UploadAsync("scan");

        var url = await _storage.GetDownloadUrlAsync(path.Value, PdfHeaders, TimeSpan.FromHours(1), CancellationToken.None);

        var expected = _clock.GetUtcNow().AddHours(1);
        url.ExpiresAt.ShouldBe(new DateTimeOffset(expected.Year, expected.Month, expected.Day, expected.Hour, expected.Minute, expected.Second, TimeSpan.Zero));
        var query = HttpUtility.ParseQueryString(url.Url.Query);
        query["sp"].ShouldBe("r"); // read only
        query["se"].ShouldBe(url.ExpiresAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        url.Url.AbsolutePath.ShouldEndWith($"/{_container}/{Uri.EscapeDataString(path.OrganisationId.ToString())}/{path.ClaimId}/{path.DocumentId}_scan.pdf");

        var response = await _browser.GetAsync(url.Url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(ClaimsApi.PdfBytes("scan"));
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Police Report.pdf");

        var overwrite = await _browser.PutAsync(url.Url, new ByteArrayContent([1, 2, 3]) { Headers = { { "x-ms-blob-type", "BlockBlob" } } });
        overwrite.StatusCode.ShouldBe(HttpStatusCode.Forbidden); // the SAS grants read only
    }

    [Fact]
    public async Task BR_D_02_An_expired_sas_is_refused_by_storage()
    {
        var path = await UploadAsync("old");
        var signedTwoHoursAgo = new AzureBlobStorageService(_blobs, _container, new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-2)));

        var url = await signedTwoHoursAgo.GetDownloadUrlAsync(path.Value, PdfHeaders, TimeSpan.FromHours(1), CancellationToken.None);

        (await _browser.GetAsync(url.Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BR_D_02_A_sas_grants_one_blob_only()
    {
        var granted = await UploadAsync("granted");
        var other = await UploadAsync("other");
        var url = await _storage.GetDownloadUrlAsync(granted.Value, PdfHeaders, TimeSpan.FromHours(1), CancellationToken.None);

        var redirected = new UriBuilder(url.Url) { Path = url.Url.AbsolutePath.Replace(granted.DocumentId.ToString(), other.DocumentId.ToString(), StringComparison.Ordinal).Replace("granted", "other", StringComparison.Ordinal) };

        (await _browser.GetAsync(redirected.Uri)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DOC_01_An_existing_blob_is_never_overwritten()
    {
        var path = await UploadAsync("original");

        var conflict = await Should.ThrowAsync<RequestFailedException>(
            () => _storage.UploadAsync(path.Value, new MemoryStream(ClaimsApi.PdfBytes("replacement")), "application/pdf", CancellationToken.None));

        conflict.Status.ShouldBe((int)HttpStatusCode.Conflict);
        var stored = await _blobs.GetBlobContainerClient(_container).GetBlobClient(path.Value).DownloadContentAsync();
        stored.Value.Content.ToArray().ShouldBe(ClaimsApi.PdfBytes("original"));
    }

    [Fact]
    public async Task DOC_09_Delete_removes_the_blob_and_tolerates_a_missing_one()
    {
        var path = await UploadAsync("orphan");

        await _storage.DeleteAsync(path.Value, CancellationToken.None);
        await _storage.DeleteAsync(path.Value, CancellationToken.None);

        (await _blobs.GetBlobContainerClient(_container).GetBlobClient(path.Value).ExistsAsync()).Value.ShouldBeFalse();
    }

    private static DocumentBlobPath NewPath(string fileName) =>
        DocumentBlobPath.For(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SanitisedFileName.From(fileName));

    private async Task<DocumentBlobPath> UploadAsync(string text)
    {
        var path = NewPath($"{text}.pdf");
        await _storage.UploadAsync(path.Value, new MemoryStream(ClaimsApi.PdfBytes(text)), "application/pdf", CancellationToken.None);
        return path;
    }
}
