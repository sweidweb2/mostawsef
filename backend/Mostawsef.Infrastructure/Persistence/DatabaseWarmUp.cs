using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Runs the everyday read queries once at startup, before the API takes requests. EF Core translates each query
/// shape on its first use and the JIT compiles the code behind it; doing that here means the first sign-in and
/// the first list of the day are as fast as every later one. Read-only, and a failure only logs a warning.
/// </summary>
public static class DatabaseWarmUp
{
    /// <summary>A search term long enough to use the same LIKE / prefix query shape as a real search.</summary>
    private const string SampleSearch = "warm-up";

    public static async Task WarmUpDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseWarmUp));
        var watch = Stopwatch.StartNew();

        try
        {
            var users = provider.GetRequiredService<IUserRepository>();
            var patients = provider.GetRequiredService<IPatientRepository>();
            var tests = provider.GetRequiredService<ILabTestRepository>();
            var orders = provider.GetRequiredService<IOrderRepository>();

            // Sign-in and the per-request token check.
            await users.GetAuthStatusAsync(Guid.Empty, cancellationToken);
            await users.GetByUsernameAsync(string.Empty, cancellationToken);

            // Each list: first page with its total, a page flip without it, and a search.
            foreach (var query in new[]
                     {
                         new PageQuery(),
                         new PageQuery(Page: 2, IncludeTotal: false),
                         new PageQuery(Search: SampleSearch),
                     })
            {
                await users.GetPageAsync(query, cancellationToken);
                await patients.GetPageAsync(query, includeUnpaid: false, cancellationToken);
                await patients.GetPageAsync(query, includeUnpaid: true, cancellationToken);
                await tests.GetPageAsync(query, active: null, cancellationToken);
                await tests.GetPageAsync(query, active: true, cancellationToken);
                await orders.GetPageAsync(query, new OrderListFilter(null, null), cancellationToken);
            }

            await orders.GetByIdAsync(Guid.Empty, cancellationToken);
            await patients.GetOrderPatientAsync(Guid.Empty, cancellationToken);

            logger.LogInformation("Warmed up the database queries in {ElapsedMs}ms", watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Database warm-up failed after {ElapsedMs}ms; the first requests may be slower", watch.ElapsedMilliseconds);
        }
    }
}
