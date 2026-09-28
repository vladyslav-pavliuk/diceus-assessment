using System.Text.Json.Serialization;
using ClaimsModule.API.Auth;
using ClaimsModule.API.Errors;
using ClaimsModule.API.Idempotency;
using ClaimsModule.API.Json;
using ClaimsModule.API.Middleware;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Auth;
using ClaimsModule.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ClaimsModule.API;

internal static class CorsPolicies
{
    public const string Spa = "Spa";
}

internal static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                // Otherwise [ApiController] rejects a missing non-nullable string with its own message before the
                // validator runs.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

                options.Filters.Add<IdempotencyFilter>();
            })
            .AddJsonOptions(options =>
            {
                // The lenient converters come first and win (D-40); JsonStringEnumConverter stays only so Swagger
                // documents enums as names.
                options.JsonSerializerOptions.Converters.Add(new LenientEnumConverterFactory());
                options.JsonSerializerOptions.Converters.Add(new LenientNullableDateTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Unbindable input (for example a malformed date) gets the same 422 body as validation.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(entry => entry.Value is { Errors.Count: > 0 })
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray(),
                            StringComparer.Ordinal);

                    return new UnprocessableEntityObjectResult(ErrorResponseFactory.Validation(errors))
                    {
                        ContentTypes = { "application/problem+json" },
                    };
                };
            });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ErrorResponseFactory.Normalise);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddJwtAuthentication();
        services.AddAuthorization(AuthorizationPolicies.Configure);

        services.AddSpaCors();
        services.AddSwagger();

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from AuthOptions, so the issuer and the validator share one key and issuer/audience pair.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, authOptions) =>
            {
                var settings = authOptions.Value;

                // Keep the short JWT claim names instead of the WS-* URIs.
                jwt.MapInboundClaims = false;

                // Lets the Hangfire dashboard read the token from the query string or its cookie (D-41).
                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = DashboardTokenHandoff.OnMessageReceived,
                    OnTokenValidated = DashboardTokenHandoff.OnTokenValidated,
                };
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = settings.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = AppClaimTypes.Name,
                    RoleClaimType = AppClaimTypes.Role,
                };
            });
    }

    private static void AddSpaCors(this IServiceCollection services)
    {
        services.AddCors();

        // No credentials: the SPA sends a Bearer header, not cookies.
        services.AddOptions<CorsOptions>()
            .Configure<IConfiguration>((cors, configuration) =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                cors.AddPolicy(CorsPolicies.Spa, policy => policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Location", IdempotencyFilter.ReplayedHeaderName));
            });
    }

    private static void AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "DICEUS Claims Module API", Version = "v1" });
            options.SupportNonNullableReferenceTypes();

            var bearerScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken from POST /api/auth/dev-token.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = JwtBearerDefaults.AuthenticationScheme },
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearerScheme] = [] });
        });
    }
}
