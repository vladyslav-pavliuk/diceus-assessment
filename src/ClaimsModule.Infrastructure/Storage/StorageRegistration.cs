using Azure.Identity;
using Azure.Storage.Blobs;
using ClaimsModule.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

internal static class StorageRegistration
{
    /// <summary>
    /// The provider is chosen at first resolve, so a test host can switch it through configuration (BR-D-03). Singletons, because
    /// each implementation caches keys.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<StorageOptions>>().Value.AzureBlob;
            var client = string.IsNullOrWhiteSpace(settings.ConnectionString)
                ? new BlobServiceClient(settings.ServiceUri, new DefaultAzureCredential())
                : new BlobServiceClient(settings.ConnectionString);
            return new AzureBlobStorageService(client, settings.ContainerName, provider.GetRequiredService<TimeProvider>());
        });

        services.AddSingleton<LocalFileSystemStorageService>();

        services.AddSingleton<IStorageService>(provider =>
            provider.GetRequiredService<IOptions<StorageOptions>>().Value.Provider switch
            {
                StorageProvider.LocalFileSystem => provider.GetRequiredService<LocalFileSystemStorageService>(),
                _ => provider.GetRequiredService<AzureBlobStorageService>(),
            });

        return services;
    }
}
