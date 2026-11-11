using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Infrastructure.Persistence.Context;
using Testcontainers.PostgreSql;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway PostgreSQL container (requires Docker).
/// Migrations and the seed users below are applied by the API's normal startup code.
/// Locally the container is reused between runs; CI always starts a fresh one.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test-only route that throws, to exercise the global exception handling. ?message=... sets the message.</summary>
    public const string ThrowPath = "/__test/throw";

    /// <summary>True in GitHub Actions and other CI; disables Postgres container reuse.</summary>
    public static bool IsCi =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
        || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    private readonly bool _reuseContainer = !IsCi;
    private readonly ConcurrentDictionary<string, string> _seededTokens = new(StringComparer.Ordinal);
    private readonly PostgreSqlContainer _postgres;

    public ApiFactory()
    {
        var builder = new PostgreSqlBuilder("postgres:17")
            .WithDatabase("mostawsef_tests")
            .WithLabel("mostawsef.test", "api-integration");

        if (_reuseContainer)
        {
            builder = builder.WithReuse(true);
        }

        _postgres = builder.Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Reused containers keep data from the previous suite; wipe so migrations/seed start clean.
        if (_reuseContainer)
        {
            await ResetPublicSchemaAsync();
        }

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

    public SqlRecorder Sql { get; } = new();

    /// <summary>
    /// Access token for <paramref name="user"/>. Seeded Admin/Super tokens are cached for the factory lifetime;
    /// dynamically created users always log in fresh (password/role may change mid-suite).
    /// </summary>
    public async Task<string> GetAccessTokenAsync(TestUser user)
    {
        if (IsSeeded(user) && _seededTokens.TryGetValue(user.Username, out var cached))
        {
            return cached;
        }

        var login = await CreateClient().LoginAsAsync(user);
        if (IsSeeded(user))
        {
            _seededTokens[user.Username] = login.AccessToken;
        }

        return login.AccessToken;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>();
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Sql));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task ResetPublicSchemaAsync()
    {
        // Defaults match PostgreSqlBuilder (user postgres) and WithDatabase("mostawsef_tests").
        var result = await _postgres.ExecAsync([
            "psql",
            "-U", "postgres",
            "-d", "mostawsef_tests",
            "-v", "ON_ERROR_STOP=1",
            "-c", "DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;",
        ]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to reset the reused Postgres schema (exit {result.ExitCode}): {result.Stderr}");
        }
    }

    private static bool IsSeeded(TestUser user) =>
        TestUsers.All.Any(seeded =>
            string.Equals(seeded.Username, user.Username, StringComparison.Ordinal)
            && string.Equals(seeded.Password, user.Password, StringComparison.Ordinal));

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
