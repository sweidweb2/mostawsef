using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogStorageTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrations_match_the_model()
    {
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Log_tables_have_a_combined_action_and_time_index_and_no_low_value_indexes()
    {
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var indexes = await db.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename IN ('activity_logs', 'system_errors')")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("IX_activity_logs_Action_OccurredAt", indexes);
        Assert.DoesNotContain("IX_activity_logs_Action", indexes);
        Assert.DoesNotContain("IX_system_errors_Level", indexes);
    }

    [Fact]
    public async Task Request_and_log_engine_contexts_come_from_a_pool()
    {
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var scoped = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var created = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(MaxPoolSize(scoped));
        Assert.NotNull(MaxPoolSize(created));
    }

    private static int? MaxPoolSize(DbContext db) =>
        db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.MaxPoolSize;
}
