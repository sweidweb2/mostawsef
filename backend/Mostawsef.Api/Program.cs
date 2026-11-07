using Mostawsef.Api.Extensions;
using Mostawsef.Application;
using Mostawsef.Infrastructure;
using Mostawsef.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
var jwtOptions = builder.Configuration.GetJwtOptions();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString, jwtOptions);
builder.Services.AddJwtAuth(jwtOptions);

var app = builder.Build();

var seedUsers = builder.Configuration.GetSection("Seed:Users").Get<List<SeedUser>>() ?? [];
await app.Services.InitializeDatabaseAsync(seedUsers);

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
