using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// The local file system is development-only: a Container App's disk is per replica and lost on restart (D-42).
/// </summary>
internal sealed class StorageOptionsValidator(IHostEnvironment environment) : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        var failures = new List<string>();
        switch (options.Provider)
        {
            case StorageProvider.AzureBlob:
                var hasConnectionString = !string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString);
                var hasServiceUri = options.AzureBlob.ServiceUri is { IsAbsoluteUri: true };
                if (hasConnectionString == hasServiceUri)
                {
                    failures.Add("Storage:AzureBlob needs exactly one of ConnectionString and ServiceUri.");
                }

                if (string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName))
                {
                    failures.Add("Storage:AzureBlob:ContainerName is required.");
                }

                break;

            case StorageProvider.LocalFileSystem:
                if (!environment.IsDevelopment())
                {
                    failures.Add("Storage:Provider LocalFileSystem is a development-only fallback; configure Storage:AzureBlob.");
                }

                if (string.IsNullOrWhiteSpace(options.LocalFileSystem.RootPath))
                {
                    failures.Add("Storage:LocalFileSystem:RootPath is required.");
                }

                if (options.LocalFileSystem.PublicBaseUrl is not { IsAbsoluteUri: true })
                {
                    failures.Add("Storage:LocalFileSystem:PublicBaseUrl must be an absolute URL.");
                }

                break;

            default:
                failures.Add("Storage:Provider must be AzureBlob or LocalFileSystem.");
                break;
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
