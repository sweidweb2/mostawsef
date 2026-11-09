using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>Bound from "Seed:Users". Users are created only if their username was never used (deleted users count).</summary>
public class SeedUser
{
    public string Username { get; set; } = string.Empty;

    /// <summary>Defaults to the username.</summary>
    public string? FullName { get; set; }

    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; }
}

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations (and their data backfills), then creates any missing seed users (passwords are hashed).</summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        IEnumerable<SeedUser> seedUsers,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (pending.Contains(SearchTextBackfill.Migration))
        {
            var rebuilt = await SearchTextBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled the search text of {Count} existing log rows", rebuilt);
        }

        if (pending.Contains(OrderOurPriceBackfill.Migration))
        {
            var priced = await OrderOurPriceBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled OurPrice on {Count} existing orders", priced);
        }

        if (pending.Contains(OrderSearchTextBackfill.Migration))
        {
            var searchable = await OrderSearchTextBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled SearchText on {Count} existing orders", searchable);
        }

        if (!await db.Set<ClinicSettings>().AnyAsync(cancellationToken))
        {
            db.Add(new ClinicSettings("Mostawsef", Currencies.Egp));
        }

        if (!await db.Set<SystemSettings>().AnyAsync(cancellationToken))
        {
            db.Add(new SystemSettings());
        }

        foreach (var seed in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Username) || string.IsNullOrWhiteSpace(seed.Password))
            {
                continue;
            }

            var username = User.NormalizeUsername(seed.Username);
            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, cancellationToken))
            {
                continue;
            }

            var fullName = string.IsNullOrWhiteSpace(seed.FullName) ? username : seed.FullName;
            db.Users.Add(new User(username, fullName, SearchText.Normalize(fullName), hasher.Hash(seed.Password), seed.Role));
            logger.LogInformation("Seeded user {Username} with role {Role}", username, seed.Role);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
