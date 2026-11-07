using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway PostgreSQL container (requires Docker).
/// Migrations and the seed users below are applied by the API's normal startup code.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("mostawsef_tests")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // The API reads these while building its host, before WebApplicationFactory overrides would apply,
        // so they are passed as environment variables.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-signing-key-0123456789abcdef");
        Environment.SetEnvironmentVariable("RateLimit__LoginPermitLimit", "1000");

        for (var i = 0; i < TestUsers.All.Length; i++)
        {
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Username", TestUsers.All[i].Username);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Password", TestUsers.All[i].Password);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Role", TestUsers.All[i].Role);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
