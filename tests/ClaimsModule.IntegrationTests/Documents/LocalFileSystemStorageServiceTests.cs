using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.IntegrationTests.Documents;

/// <summary>
/// The local fallback on its own (BR-D-03, DOC-03): its download token behaves like a SAS (expires, cannot be forged or
/// edited, dies with the process), and it refuses any object path that could leave its root, whoever calls it.
/// </summary>
public sealed class LocalFileSystemStorageServiceTests : IDisposable
{
    private const string ObjectPath = "11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/33333333-3333-3333-3333-333333333333_notes.txt";
    private static readonly DownloadHeaders TextHeaders = new("text/plain", "notes.txt", Inline: false);

    // The root sits inside a directory of its own, so a path that escaped the root would land in "_sandbox", be seen by the
    // test, and be cleaned up with it.
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "claims-module-tests", Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero));
    private readonly LocalFileSystemStorageService _storage;

    public LocalFileSystemStorageServiceTests()
    {
        _root = Path.Combine(_sandbox, "root");
        _storage = NewService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task BR_D_02_Local_link_is_valid_for_its_lifetime_and_not_a_second_longer()
    {
        await UploadAsync("hello");
        var url = await _storage.GetDownloadUrlAsync(ObjectPath, TextHeaders, TimeSpan.FromHours(1), CancellationToken.None);
        var token = Token(url);

        url.ExpiresAt.ShouldBe(_clock.GetUtcNow().AddHours(1));
        _storage.OpenDownload(token).ShouldNotBeNull().ContentDisposition.ShouldBe("attachment; filename=\"notes.txt\"; filename*=UTF-8''notes.txt");

        _clock.Advance(TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1));
        _storage.OpenDownload(token).ShouldNotBeNull();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _storage.OpenDownload(token).ShouldBeNull();
    }

    [Fact]
    public async Task BR_D_03_A_token_cannot_be_edited_or_forged_and_dies_with_the_process()
    {
        await UploadAsync("hello");
        var token = Token(await _storage.GetDownloadUrlAsync(ObjectPath, TextHeaders, TimeSpan.FromHours(1), CancellationToken.None));
        var payload = token.Split('.')[0];

        // Another payload (e.g. another path or a later expiry) with the old signature.
        var edited = System.Buffers.Text.Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(System.Buffers.Text.Base64Url.DecodeFromChars(payload)).Replace("notes.txt\",\"ExpiresAt", "other.txt\",\"ExpiresAt", StringComparison.Ordinal)));
        _storage.OpenDownload($"{edited}.{token.Split('.')[1]}").ShouldBeNull();
        _storage.OpenDownload(payload).ShouldBeNull();
        _storage.OpenDownload("").ShouldBeNull();
        _storage.OpenDownload("a.b.c").ShouldBeNull();

        NewService().OpenDownload(token).ShouldBeNull(); // a restarted API has a new key
    }

    [Fact]
    public async Task BR_D_03_A_link_to_a_deleted_file_is_refused()
    {
        await UploadAsync("hello");
        var token = Token(await _storage.GetDownloadUrlAsync(ObjectPath, TextHeaders, TimeSpan.FromHours(1), CancellationToken.None));

        await _storage.DeleteAsync(ObjectPath, CancellationToken.None);
        await _storage.DeleteAsync(ObjectPath, CancellationToken.None); // deleting a missing file is not an error

        _storage.OpenDownload(token).ShouldBeNull();
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("org/../../outside.txt")]
    [InlineData("org/claim/../../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("org\\..\\..\\outside.txt")]
    [InlineData("org//outside.txt")]
    [InlineData("./outside.txt")]
    [InlineData("")]
    public async Task BR_D_01_Object_paths_that_could_leave_the_root_are_refused(string objectPath)
    {
        await Should.ThrowAsync<ArgumentException>(() => _storage.UploadAsync(objectPath, new MemoryStream([1]), "text/plain", CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(() => _storage.DeleteAsync(objectPath, CancellationToken.None));
        (Directory.Exists(_sandbox) ? Directory.GetFiles(_sandbox, "*", SearchOption.AllDirectories) : []).ShouldBeEmpty();
    }

    [Fact]
    public async Task DOC_01_An_existing_file_is_never_overwritten()
    {
        await UploadAsync("original");

        await Should.ThrowAsync<IOException>(() => UploadAsync("replacement"));

        (await File.ReadAllTextAsync(Path.Combine(_storage.RootPath, ObjectPath))).ShouldBe("original");
    }

    private LocalFileSystemStorageService NewService() => new(
        Options.Create(new StorageOptions
        {
            Provider = StorageProvider.LocalFileSystem,
            LocalFileSystem = new LocalFileSystemStorageOptions { RootPath = _root, PublicBaseUrl = new Uri("http://localhost:5080") },
        }),
        new TestEnvironment(),
        _clock);

    private Task UploadAsync(string text) =>
        _storage.UploadAsync(ObjectPath, new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/plain", CancellationToken.None);

    private static string Token(SignedDownloadUrl url) => url.Url.AbsolutePath[("/" + LocalFileSystemStorageService.DownloadRoutePrefix).Length..];

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "ClaimsModule.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
