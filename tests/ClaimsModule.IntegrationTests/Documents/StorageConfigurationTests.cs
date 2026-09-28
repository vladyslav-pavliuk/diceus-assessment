using ClaimsModule.Application.Abstractions;
using ClaimsModule.Infrastructure.Storage;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClaimsModule.IntegrationTests.Documents;

/// <summary>BR-D-03 / DOC-04: IStorageService is selected by Storage:Provider, and a configuration that cannot work refuses to start (D-42).</summary>
[Collection(ApiCollection.Name)]
public sealed class StorageConfigurationTests(ApiFixture fixture)
{
    [Fact]
    public async Task BR_D_03_Provider_is_selected_from_configuration()
    {
        fixture.Factory.Services.GetRequiredService<IStorageService>().ShouldBeOfType<LocalFileSystemStorageService>(); // Development default

        var azure = await fixture.AzureBlobHostAsync();
        azure.Services.GetRequiredService<IStorageService>().ShouldBeOfType<AzureBlobStorageService>();
    }

    [Theory]
    [InlineData("LocalFileSystem", null, "Storage:Provider LocalFileSystem is a development-only fallback; configure Storage:AzureBlob.")]
    [InlineData("AzureBlob", null, "Storage:AzureBlob needs exactly one of ConnectionString and ServiceUri.")]
    [InlineData("AzureBlob", "UseDevelopmentStorage=true", "Storage:AzureBlob needs exactly one of ConnectionString and ServiceUri.")] // both
    public void BR_D_03_Outside_development_an_unusable_storage_configuration_refuses_to_start(string provider, string? connectionString, string message)
    {
        using var production = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Auth:SigningKey", "production-like-signing-key-for-this-test-only");
            builder.UseSetting("Storage:Provider", provider);
            builder.UseSetting("Storage:AzureBlob:ConnectionString", connectionString);
            builder.UseSetting("Storage:AzureBlob:ServiceUri", connectionString is null ? null : "https://claimsdocs.blob.core.windows.net");
        });

        Should.Throw<OptionsValidationException>(() => production.CreateClient()).Failures.ShouldContain(message);
    }
}
