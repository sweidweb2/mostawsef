using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Api.Middleware;
using Mostawsef.Api.Services;
using Mostawsef.Application;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Contracts.Common;
using Mostawsef.Infrastructure;
using Mostawsef.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Production first-run: load/create appsettings.Clinic.json with a unique Jwt:Key (never committed).
Mostawsef.Api.ClinicLocalConfig.Apply(builder);

// The address the desktop app expects by default, used only when nothing else names one (launch profile,
// ASPNETCORE_URLS, --urls, "Urls" or Kestrel endpoints in configuration), so those can always change it.
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) && !builder.Configuration.GetSection("Kestrel:Endpoints").Exists())
{
    builder.WebHost.UseUrls("http://localhost:5270");
}

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

// Compile-time JSON metadata for the contracts (no reflection on each type's first use); reflection stays as the fallback.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, ContractsJsonContext.Default));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Automatic 400s from [ApiController] model validation: their "errors" values are ErrorCodes (attribute ErrorMessage).
    if (context.ProblemDetails is ValidationProblemDetails)
    {
        context.ProblemDetails.Extensions.TryAdd(ProblemFields.Code, ErrorCodes.Validation);
    }
});
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddJwtAuth();
builder.Services.AddOptions<LogRetentionOptions>()
    .BindConfiguration(LogRetentionOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ScheduledBackupOptions>()
    .BindConfiguration(ScheduledBackupOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<LogRetentionWorker>();
builder.Services.AddHostedService<ScheduledBackupWorker>();

// Behind a reverse proxy the client address is in X-Forwarded-For; without this every client shares one rate-limit bucket.
// Only loopback proxies are trusted unless ForwardedHeaders:KnownProxies is configured (the defaults of ForwardedHeadersOptions).
var useForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
}

var app = builder.Build();

var seedUsers = builder.Configuration.GetSection("Seed:Users").Get<List<SeedUser>>() ?? [];
await app.Services.InitializeDatabaseAsync(seedUsers);
await app.Services.WarmUpDatabaseAsync();

if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<RequestTimingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ActivityLogFlushMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No HTTPS redirection: the desktop app calls the API over plain HTTP, and a redirect drops the Authorization header.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Entry point type, referenced by WebApplicationFactory in integration tests.</summary>
public partial class Program;
