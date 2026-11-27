using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
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
        Environment.SetEnvironmentVariable("RateLimit__ClinicSessionPermitLimit", "1000");
        Environment.SetEnvironmentVariable("RateLimit__LoginFailuresPerUser", "1000");
        Environment.SetEnvironmentVariable("RateLimit__HealthPermitLimit", "100000");

        for (var i = 0; i < TestUsers.SeededLogins.Length; i++)
        {
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Username", TestUsers.SeededLogins[i].Username);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Password", TestUsers.SeededLogins[i].Password);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Role", TestUsers.SeededLogins[i].Role);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__MustChangePassword", "false");
        }

        // Seeded like a clinic install (no MustChangePassword setting, so it defaults to true). Only
        // InitialPasswordTests signs in with it.
        var initial = TestUsers.SeededLogins.Length;
        Environment.SetEnvironmentVariable($"Seed__Users__{initial}__Username", TestUsers.InitialSuper.Username);
        Environment.SetEnvironmentVariable($"Seed__Users__{initial}__Password", TestUsers.InitialSuper.Password);
        Environment.SetEnvironmentVariable($"Seed__Users__{initial}__Role", TestUsers.InitialSuper.Role);
        Environment.SetEnvironmentVariable($"Seed__Users__{initial}__MustChangePassword", null);

        // Drop leftover seed slots from older test runs in this process.
        for (var i = initial + 1; i < 4; i++)
        {
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Username", null);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Password", null);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__Role", null);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__FullName", null);
            Environment.SetEnvironmentVariable($"Seed__Users__{i}__MustChangePassword", null);
        }
    }

    public SqlRecorder Sql { get; } = new();

    /// <summary>
    /// Access token for <paramref name="user"/>. Seeded Super and clinic tokens are cached for the factory lifetime;
    /// dynamically created users always log in fresh (password/role may change mid-suite).
    /// </summary>
    public async Task<string> GetAccessTokenAsync(TestUser user)
    {
        if (IsCachedIdentity(user) && _seededTokens.TryGetValue(user.Username, out var cached))
        {
            return cached;
        }

        var login = user.Role == Roles.Admin || string.Equals(user.Username, ClinicIdentity.Username, StringComparison.Ordinal)
            ? await CreateClient().OpenClinicSessionAsync()
            : await CreateClient().LoginAsAsync(user);
        if (IsCachedIdentity(user))
        {
            _seededTokens[user.Username] = login.AccessToken;
        }

        return login.AccessToken;
    }

    /// <summary>
    /// Drops cached Admin/Super JWTs. Call after a test bumps a seeded user's <c>TokenVersion</c>
    /// (e.g. clinic identity repair) so later tests do not reuse a rejected token.
    /// </summary>
    public void InvalidateSeededAccessTokens() => _seededTokens.Clear();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>();
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(Sql));
            // Default backup folder is next to the API; avoid real pg_dump during every suite start.
            services.RemoveAll<IDatabaseDumpRunner>();
            services.AddSingleton<IDatabaseDumpRunner, SilentDumpRunner>();
        });
    }

    /// <summary>Writes an empty dump file so scheduled-backup startup does not need PostgreSQL client tools.</summary>
    private sealed class SilentDumpRunner : IDatabaseDumpRunner
    {
        public Task RunAsync(string outputFilePath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
            return File.WriteAllBytesAsync(outputFilePath, [0x50, 0x47, 0x44, 0x4D], cancellationToken);
        }
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

    private static bool IsCachedIdentity(TestUser user) =>
        string.Equals(user.Username, TestUsers.Admin.Username, StringComparison.Ordinal)
        || TestUsers.SeededLogins.Any(seeded =>
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
