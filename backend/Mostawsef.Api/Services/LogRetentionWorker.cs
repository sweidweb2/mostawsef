using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Logs;

namespace Mostawsef.Api.Services;

/// <summary>Purges expired activity logs and system errors when the API starts, then once a day.</summary>
public class LogRetentionWorker(IServiceScopeFactory scopes) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<ILogRetentionService>().PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await scope.ServiceProvider.GetRequiredService<ISystemErrorLogger>()
                    .LogAsync(ex, details: new { Job = nameof(LogRetentionWorker) }, cancellationToken: stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
