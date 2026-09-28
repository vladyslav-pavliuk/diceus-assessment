using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Documents;

/// <summary>
/// Documents through HTTP (FRS §10.1, §13, D-28, D-42) on the Development host, whose provider is the local file system
/// (BR-D-03). The same endpoints against Azure Blob Storage (Azurite) are in <see cref="AzureBlobDocumentTests"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DocumentEndpointTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_08_Upload_stores_the_file_under_org_and_claim_and_records_the_metadata()
    {
        var claim = await _api.CreateClaimAsync();
        var content = ClaimsApi.PdfBytes("scene photos");

        var response = await _api.UploadDocumentAsync(claim.Id, "Police Report.pdf", content, documentType: "PoliceReport", notes: "From the scene.");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var document = (await response.Content.ReadFromJsonAsync<DocumentDto>(TestAuth.Json))!;
        document.DocumentName.ShouldBe("Police Report.pdf");
        document.DocumentType.ShouldBe(DocumentType.PoliceReport);
        document.ContentType.ShouldBe("application/pdf");
        document.FileSizeBytes.ShouldBe(content.Length);
        document.UploadedByName.ShouldBe("Alex Carter");
        document.Notes.ShouldBe("From the scene.");

        // BR-D-01 / DOC-03: {root}/{organisationId}/{claimId}/{documentId}_{name}, byte for byte.
        var organisationId = await new TestDatabase(fixture).SeededOrganisationIdAsync();
        var expectedPath = $"{organisationId}/{claim.Id}/{document.Id}_Police Report.pdf";
        (await File.ReadAllBytesAsync(Path.Combine(fixture.Factory.UploadsRoot, expectedPath))).ShouldBe(content);
        (await BlobPathAsync(document.Id)).ShouldBe(expectedPath); // DOC-08: the metadata row

        (await _api.GetDetailAsync(claim.Id)).Documents.ShouldHaveSingleItem().Id.ShouldBe(document.Id); // API-03
    }

    [Fact]
    public async Task DOC_07_Upload_writes_DOCUMENT_UPLOADED_with_the_document_id()
    {
        var claim = await _api.CreateClaimAsync();

        var document = await _api.UploadDocumentOkAsync(claim.Id, "invoice.pdf", documentType: "Invoice");

        var entry = (await _api.AuditAsync(claim.Id)).First();
        entry.EventType.ShouldBe(AuditEventTypes.DocumentUploaded);
        entry.RelatedEntityId.ShouldBe(document.Id);
        entry.RelatedEntityType.ShouldBe("ClaimDocument");
        entry.CreatedByName.ShouldBe("Alex Carter");
        var newValue = JsonDocument.Parse(entry.NewValue!).RootElement;
        newValue.GetProperty("documentName").GetString().ShouldBe("invoice.pdf");
        newValue.GetProperty("documentType").GetString().ShouldBe("Invoice");
        newValue.GetProperty("contentType").GetString().ShouldBe("application/pdf");
        newValue.GetProperty("fileSizeBytes").GetInt64().ShouldBe(document.FileSizeBytes);
    }

    [Fact]
    public async Task API_09_List_returns_every_document_newest_first_with_a_one_hour_download_url()
    {
        var claim = await _api.CreateClaimAsync();
        var first = await _api.UploadDocumentOkAsync(claim.Id, "first.pdf");
        var second = await _api.UploadDocumentOkAsync(claim.Id, "costs.csv", Encoding.UTF8.GetBytes("item,amount\nbumper,1200\n"));

        var before = DateTimeOffset.UtcNow;
        var documents = await _api.ListDocumentsAsync(claim.Id);

        documents.Select(document => document.Id).ShouldBe([second.Id, first.Id]);
        documents.ShouldAllBe(document => document.DownloadUrl.IsAbsoluteUri);
        foreach (var document in documents)
        {
            // BR-D-02: 1-hour TTL (URL expiry has whole-second precision).
            document.DownloadUrlExpiresAt.ShouldBeInRange(before.AddHours(1).AddSeconds(-2), DateTimeOffset.UtcNow.AddHours(1).AddSeconds(1));
        }
    }

    /// <summary>
    /// DOC-03: the local fallback's link opens in a new tab without a Bearer token, like a SAS, and fixes the response
    /// headers: the canonical type, inline for PDF, attachment for text, and nosniff.
    /// </summary>
    [Fact]
    public async Task BR_D_03_Local_download_link_serves_the_bytes_with_the_stored_headers()
    {
        var claim = await _api.CreateClaimAsync();
        var pdf = await _api.UploadDocumentOkAsync(claim.Id, "Résumé scan.pdf", ClaimsApi.PdfBytes("résumé"));
        var text = await _api.UploadDocumentOkAsync(claim.Id, "notes.txt", Encoding.UTF8.GetBytes("<html>not rendered</html>"));
        var anonymous = fixture.Factory.CreateClient();

        var pdfResponse = await anonymous.GetAsync(pdf.DownloadUrl);
        pdfResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pdfResponse.Content.ReadAsByteArrayAsync()).ShouldBe(ClaimsApi.PdfBytes("résumé"));
        pdfResponse.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        pdfResponse.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        pdfResponse.Content.Headers.ContentDisposition.FileNameStar.ShouldBe("Résumé scan.pdf");
        pdfResponse.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);

        var textResponse = await anonymous.GetAsync(text.DownloadUrl);
        textResponse.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        textResponse.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task BR_D_03_A_tampered_or_invented_download_link_is_refused()
    {
        var claim = await _api.CreateClaimAsync();
        var document = await _api.UploadDocumentOkAsync(claim.Id);
        var anonymous = fixture.Factory.CreateClient();
        var url = document.DownloadUrl.ToString();
        var signature = url[(url.LastIndexOf('.') + 1)..];

        var tampered = url[..^signature.Length] + (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        (await anonymous.GetAsync(tampered)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/local-files/not-a-token")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task API_27_Fresh_download_url_and_404_for_an_unknown_document()
    {
        var claim = await _api.CreateClaimAsync();
        var document = await _api.UploadDocumentOkAsync(claim.Id);

        var response = await _api.Client.GetAsync($"/api/claims/{claim.Id}/documents/{document.Id}/url");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fresh = (await response.Content.ReadFromJsonAsync<DocumentDownloadUrlDto>(TestAuth.Json))!;
        fresh.DocumentId.ShouldBe(document.Id);
        (await fixture.Factory.CreateClient().GetAsync(fresh.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _api.Client.GetAsync($"/api/claims/{claim.Id}/documents/{Guid.NewGuid()}/url")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _api.Client.GetAsync($"/api/claims/{Guid.NewGuid()}/documents")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// BR-D-01 over HTTP: whatever the multipart file name, the file lands in the claim's folder, under the sanitised name,
    /// and nothing is written anywhere else under the storage root.
    /// </summary>
    [Theory]
    [InlineData("../../../../etc/cron.d/evil.txt", "evil.txt")]
    [InlineData("..\\..\\..\\Windows\\evil.txt", "evil.txt")]
    [InlineData("/etc/passwd.txt", "passwd.txt")]
    [InlineData("C:\\Windows\\System32\\evil.txt", "evil.txt")]
    [InlineData("\uFF0E\uFF0E\uFF0F\uFF0E\uFF0E\uFF0Fevil.txt", "evil.txt")] // fullwidth "．．／"
    [InlineData("in\u202Evoice.txt", "invoice.txt")] // right-to-left override
    public async Task BR_D_01_Traversal_file_names_are_stored_inside_the_claim_folder(string fileName, string storedName)
    {
        var claim = await _api.CreateClaimAsync();

        var response = await _api.UploadDocumentAsync(claim.Id, fileName, Encoding.UTF8.GetBytes("plain text"), "text/plain");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var document = (await response.Content.ReadFromJsonAsync<DocumentDto>(TestAuth.Json))!;
        document.DocumentName.ShouldBe(storedName);

        var organisationId = await new TestDatabase(fixture).SeededOrganisationIdAsync();
        var claimFolder = Path.Combine(fixture.Factory.UploadsRoot, organisationId.ToString(), claim.Id.ToString());
        Directory.GetFiles(claimFolder).Select(Path.GetFileName).ShouldBe([$"{document.Id}_{storedName}"]);
        Directory.GetFiles(fixture.Factory.UploadsRoot, "*evil*", SearchOption.AllDirectories)
            .ShouldAllBe(path => path.StartsWith(Path.Combine(fixture.Factory.UploadsRoot, organisationId.ToString()), StringComparison.Ordinal));
    }

    public static TheoryData<string, string, byte[], string> RejectedFiles => new()
    {
        { "setup.exe", "application/x-msdownload", [0x4D, 0x5A, 0x90, 0x00], DocumentFormat.NotAllowedMessage },
        { "page.html", "text/html", Encoding.UTF8.GetBytes("<html></html>"), DocumentFormat.NotAllowedMessage },
        { "invoice.pdf", "application/pdf", [0x4D, 0x5A, 0x90, 0x00], "The file content does not match its type." },
        { "photo.png", "image/png", [0xFF, 0xD8, 0xFF, 0xE0], "The file content does not match its type." },
        { "notes.txt", "text/html", Encoding.UTF8.GetBytes("<html></html>"), DocumentFormat.DeclaredTypeMismatchMessage },
        { "empty.pdf", "application/pdf", [], "The file is empty." },
    };

    /// <summary>DOC-05: the allowlist is enforced on the extension, the declared type and the bytes; a refused file is never stored.</summary>
    [Theory]
    [MemberData(nameof(RejectedFiles))]
    public async Task DOC_05_A_file_outside_the_allowlist_or_with_spoofed_content_is_rejected_and_not_stored(
        string fileName, string contentType, byte[] content, string message)
    {
        var claim = await _api.CreateClaimAsync();

        var response = await _api.UploadDocumentAsync(claim.Id, fileName, content, contentType);

        (await ClaimsApi.ErrorsAsync(response))["File"].ShouldBe([message]);
        await ShouldHaveNoStoredFilesAsync(claim.Id);
    }

    [Fact]
    public async Task DOC_06_A_file_over_50_MB_is_rejected()
    {
        var claim = await _api.CreateClaimAsync();
        var content = new byte[ClaimDocument.MaxFileSizeBytes + 1];
        "%PDF-"u8.CopyTo(content);

        var response = await _api.UploadDocumentAsync(claim.Id, "huge.pdf", content);

        (await ClaimsApi.ErrorsAsync(response))["File"].ShouldBe(["The file must not exceed 50 MB."]);
        await ShouldHaveNoStoredFilesAsync(claim.Id);
    }

    /// <summary>A body over the endpoint's 51 MB limit is refused from its Content-Length, before the form is read (D-42 item 6).</summary>
    [Fact]
    public async Task DOC_06_A_body_over_the_endpoint_limit_is_413()
    {
        var claim = await _api.CreateClaimAsync();
        var content = new byte[ClaimDocument.MaxFileSizeBytes + (2 * 1024 * 1024)];
        "%PDF-"u8.CopyTo(content);

        var response = await _api.UploadDocumentAsync(claim.Id, "huge.pdf", content, idempotencyKey: Guid.NewGuid().ToString());

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "PayloadTooLarge");
        body.GetProperty("title").GetString().ShouldBe("The request body is too large.");
        await ShouldHaveNoStoredFilesAsync(claim.Id);
    }

    [Fact]
    public async Task API_08_A_missing_file_is_422_and_a_json_body_is_415()
    {
        var claim = await _api.CreateClaimAsync();

        var noFile = await _api.Client.PostAsync($"/api/claims/{claim.Id}/documents", new MultipartFormDataContent { { new StringContent("Invoice"), "documentType" } });
        (await ClaimsApi.ErrorsAsync(noFile))["File"].ShouldBe(["A file is required."]);

        var json = await _api.Client.PostAsJsonAsync($"/api/claims/{claim.Id}/documents", new { fileName = "a.pdf" });
        json.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task TR_13_A_document_cannot_be_added_to_a_withdrawn_claim()
    {
        var claim = await _api.CreateOpenClaimAsync();
        await _api.TransitionOkAsync(claim.Id, "Withdrawn", reason: "Duplicate notification.");

        var response = await _api.UploadDocumentAsync(claim.Id, "late.pdf", ClaimsApi.PdfBytes());

        (await ClaimsApi.ErrorsAsync(response))["Claim"].ShouldBe(["Claim is Withdrawn; no changes are permitted."]);
        await ShouldHaveNoStoredFilesAsync(claim.Id);
    }

    [Fact]
    public async Task SEC_04_Another_organisations_claim_has_no_documents_to_see_or_add_to()
    {
        var foreign = await _api.CreateClaimAsync();
        var otherOrganisation = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            await dbContext.Database.ExecuteSqlAsync(
                $"INSERT INTO [Organisations] ([Id], [Name], [CreatedAt]) VALUES ({otherOrganisation}, N'Document Rival', SYSDATETIMEOFFSET())");
            await dbContext.Database.ExecuteSqlAsync($"UPDATE [Claims] SET [OrganisationId] = {otherOrganisation} WHERE [Id] = {foreign.Id}");
        }

        (await _api.UploadDocumentAsync(foreign.Id, "a.pdf", ClaimsApi.PdfBytes())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _api.Client.GetAsync($"/api/claims/{foreign.Id}/documents")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Directory.GetFiles(fixture.Factory.UploadsRoot, "*", SearchOption.AllDirectories)
            .ShouldNotContain(path => path.Contains(foreign.Id.ToString(), StringComparison.Ordinal));
    }

    /// <summary>DOC-09 end to end: the blob was written, the metadata transaction failed, so the blob is removed and no row or audit entry exists.</summary>
    [Fact]
    public async Task DOC_09_A_failed_commit_leaves_no_file_no_row_and_no_audit_entry()
    {
        var claim = await _api.CreateClaimAsync();

        var response = await _api.UploadDocumentAsync(claim.Id, $"{FailingDocumentCommit.Prefix}.pdf", ClaimsApi.PdfBytes());

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await ShouldHaveNoStoredFilesAsync(claim.Id);
        (await _api.ListDocumentsAsync(claim.Id)).ShouldBeEmpty();
        (await _api.AuditAsync(claim.Id)).ShouldNotContain(entry => entry.EventType == AuditEventTypes.DocumentUploaded);
    }

    /// <summary>API-IDEMP for multipart: a retried upload with the same Idempotency-Key replays the first response and stores nothing new.</summary>
    [Fact]
    public async Task API_IDEMP_A_repeated_upload_with_the_same_key_stores_one_document()
    {
        var claim = await _api.CreateClaimAsync();
        var key = Guid.NewGuid().ToString();

        var first = await _api.UploadDocumentAsync(claim.Id, "invoice.pdf", ClaimsApi.PdfBytes(), idempotencyKey: key);
        var replay = await _api.UploadDocumentAsync(claim.Id, "invoice.pdf", ClaimsApi.PdfBytes(), idempotencyKey: key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").ShouldBe(["true"]);
        (await replay.Content.ReadFromJsonAsync<DocumentDto>(TestAuth.Json))!.Id
            .ShouldBe((await first.Content.ReadFromJsonAsync<DocumentDto>(TestAuth.Json))!.Id);
        (await _api.ListDocumentsAsync(claim.Id)).ShouldHaveSingleItem();
    }

    /// <summary>The form is hashed by content, so the key still refuses a different file (D-24 mismatch → 422).</summary>
    [Fact]
    public async Task API_IDEMP_The_same_key_with_a_different_file_is_rejected()
    {
        var claim = await _api.CreateClaimAsync();
        var key = Guid.NewGuid().ToString();

        (await _api.UploadDocumentAsync(claim.Id, "invoice.pdf", ClaimsApi.PdfBytes("one"), idempotencyKey: key)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var other = await _api.UploadDocumentAsync(claim.Id, "invoice.pdf", ClaimsApi.PdfBytes("two"), idempotencyKey: key);

        (await ClaimsApi.ErrorsAsync(other))["Idempotency-Key"].ShouldBe(["Idempotency-Key reuse with a different request."]);
        (await _api.ListDocumentsAsync(claim.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SEC_01_Uploading_requires_a_token()
    {
        var claim = await _api.CreateClaimAsync();
        var anonymous = new ClaimsApi(fixture.Factory.CreateClient());

        (await anonymous.UploadDocumentAsync(claim.Id, "a.pdf", ClaimsApi.PdfBytes())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await ShouldHaveNoStoredFilesAsync(claim.Id);
    }

    private async Task ShouldHaveNoStoredFilesAsync(Guid claimId)
    {
        var organisationId = await new TestDatabase(fixture).SeededOrganisationIdAsync();
        var claimFolder = Path.Combine(fixture.Factory.UploadsRoot, organisationId.ToString(), claimId.ToString());
        (Directory.Exists(claimFolder) ? Directory.GetFiles(claimFolder) : []).ShouldBeEmpty();
    }

    private async Task<string> BlobPathAsync(Guid documentId)
    {
        await using var scope = await new TestDatabase(fixture).TenantScopeAsync();
        return await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Set<ClaimDocument>()
            .Where(document => document.Id == documentId).Select(document => document.BlobPath).SingleAsync();
    }
}
