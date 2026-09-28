using System.Net;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Documents;

/// <summary>
/// The document endpoints on an API host whose Storage:Provider is AzureBlob, against Azurite (BR-D-03: the provider is
/// chosen by configuration alone; BR-D-02: the listed URL is a SAS on Storage, and the API never serves the bytes).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AzureBlobDocumentTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApiFactory _host = null!;
    private ClaimsApi _api = null!;

    public async Task InitializeAsync()
    {
        _host = await fixture.AzureBlobHostAsync();
        _api = await ClaimsApi.SignInAsync(_host, "handler.alex");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BR_D_02_Uploaded_document_is_listed_with_a_sas_that_downloads_from_storage()
    {
        var claim = await _api.CreateClaimAsync();
        var content = ClaimsApi.PdfBytes("azure");

        var uploaded = await _api.UploadDocumentOkAsync(claim.Id, "Police Report.pdf", content);
        var listed = (await _api.ListDocumentsAsync(claim.Id)).ShouldHaveSingleItem();

        listed.Id.ShouldBe(uploaded.Id);
        listed.DownloadUrl.AbsolutePath.ShouldContain("/claim-documents/");
        listed.DownloadUrl.Query.ShouldContain("sig=");
        listed.DownloadUrl.Authority.ShouldNotBe(_host.Server.BaseAddress.Authority); // Storage, not the API

        using var browser = new HttpClient();
        var response = await browser.GetAsync(listed.DownloadUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(content);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task BR_D_03_The_local_download_endpoint_does_not_exist_with_azure_storage()
    {
        var response = await _api.Client.GetAsync("/api/local-files/anything");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
