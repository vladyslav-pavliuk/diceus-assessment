using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Auth;
using ClaimsModule.Infrastructure.Auth;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Infrastructure.Correlation;
using ClaimsModule.Infrastructure.Storage;
using ClaimsModule.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // The only clock in the application. Tests replace it with FakeTimeProvider.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<CorrelationContext>());

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Auth:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Auth:Audience is required.")
            .Validate(
                options => options.SigningKey.Length >= AuthOptions.MinimumSigningKeyLength,
                $"Auth:SigningKey must be at least {AuthOptions.MinimumSigningKeyLength} characters (HS256).")
            .Validate(options => options.TokenLifetime > TimeSpan.Zero, "Auth:TokenLifetime must be positive.")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddBackgroundJobs(configuration);
        services.AddStorage(configuration);

        return services;
    }
}
