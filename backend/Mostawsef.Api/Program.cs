using Mostawsef.Api.Extensions;
using Mostawsef.Api.Middleware;
using Mostawsef.Api.Services;
using Mostawsef.Application;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure;
using Mostawsef.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
var jwtOptions = builder.Configuration.GetJwtOptions();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, jwtOptions);
builder.Services.AddJwtAuth(jwtOptions);

var app = builder.Build();

var seedUsers = builder.Configuration.GetSection("Seed:Users").Get<List<SeedUser>>() ?? [];
await app.Services.InitializeDatabaseAsync(seedUsers);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Entry point type, referenced by WebApplicationFactory in integration tests.</summary>
public partial class Program;
