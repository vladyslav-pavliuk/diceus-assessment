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

// Readable text locally, one JSON object per line elsewhere (for Log Analytics).
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

// The correlation id must exist before anything logs, and request logging sits outside exception handling so
// it records the final status code.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();

// On in every environment: the brief requires the deployed API to expose it.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicies.Spa);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Only mapped for the local storage fallback (D-28).
app.MapLocalFileDownloads();

// Read-only (D-41): a failed posting can only be retried through the audited endpoint, and there are no
// cookie-authenticated POSTs to protect against CSRF.
app.MapHangfireDashboard(DashboardTokenHandoff.Path, new DashboardOptions
{
    Authorization = [],
    IsReadOnlyFunc = _ => true,
    DashboardTitle = "Claims Module jobs",
    DisplayStorageConnectionString = false,
    AppPath = null,
}).RequireAuthorization(AuthorizationPolicies.Manager);

// Liveness has no dependencies, so a paused serverless database never restarts the container.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
}).AllowAnonymous();

app.Run();

/// <summary>Public for WebApplicationFactory.</summary>
public partial class Program;
