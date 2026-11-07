using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Api.Middleware;
using Mostawsef.Api.Services;
using Mostawsef.Application;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Contracts.Common;
using Mostawsef.Infrastructure;
using Mostawsef.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddControllers();
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
builder.Services.AddHostedService<LogRetentionWorker>();

var app = builder.Build();

var seedUsers = builder.Configuration.GetSection("Seed:Users").Get<List<SeedUser>>() ?? [];
await app.Services.InitializeDatabaseAsync(seedUsers);

app.UseMiddleware<ExceptionHandlingMiddleware>();

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
