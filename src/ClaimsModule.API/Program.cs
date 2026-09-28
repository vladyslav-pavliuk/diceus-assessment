using ClaimsModule.API;
using ClaimsModule.API.Auth;
using ClaimsModule.API.Middleware;
using ClaimsModule.API.Storage;
using ClaimsModule.Application;
using ClaimsModule.Infrastructure;
using ClaimsModule.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. Levels come from the "Serilog" configuration section; the sink is chosen here:
// readable text locally, one JSON object per line elsewhere (Container Apps → Log Analytics).
builder.Host.UseSerilog((context, services, logger) =>
{
    logger.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsDevelopment())
    {
        logger.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
    else
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPersistence()
    .AddApi();

var app = builder.Build();

// Order matters: the correlation id must exist before anything logs, and request logging sits
// outside exception handling so it records the final status code.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();

// Swagger stays on in every environment: the deployed API must expose it (brief §4.3).
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicies.Spa);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Development-only download endpoint of the local document storage fallback (FRS §13, D-28); absent with Azure Blob Storage.
app.MapLocalFileDownloads();

// Hangfire dashboard (D-41): managers only, through the normal authorization pipeline (the token may arrive as
// ?access_token= once, then as a cookie; see DashboardTokenHandoff). Read-only: no requeue or delete buttons, so
// the only way to post a failed GL posting again is the audited retry endpoint, and cookie-authenticated POSTs
// (a CSRF surface) do not exist.
app.MapHangfireDashboard(DashboardTokenHandoff.Path, new DashboardOptions
{
    Authorization = [],
    IsReadOnlyFunc = _ => true,
    DashboardTitle = "Claims Module jobs",
    DisplayStorageConnectionString = false,
    AppPath = null,
}).RequireAuthorization(AuthorizationPolicies.Manager);

// Liveness has no dependencies, so a paused serverless database never restarts the container.
// Readiness includes the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
}).AllowAnonymous();

app.Run();

/// <summary>Entry point; public so that WebApplicationFactory can host the API in integration tests.</summary>
public partial class Program;
