using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway PostgreSQL container (requires Docker).
/// Migrations and the seed users below are applied by the API's normal startup code.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test-only route that throws, to exercise the global exception handling. ?message=... sets the message.</summary>
    public const string ThrowPath = "/__test/throw";

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Appended after the app's own pipeline, so the app's exception middleware wraps it.</summary>
    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use((HttpContext context, RequestDelegate nextMiddleware) =>
                context.Request.Path == ThrowPath
                    ? throw new InvalidOperationException(context.Request.Query["message"].ToString())
                    : nextMiddleware(context));
        };
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
