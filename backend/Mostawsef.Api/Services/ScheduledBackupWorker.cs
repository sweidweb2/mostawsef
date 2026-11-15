using Microsoft.Extensions.Options;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;

namespace Mostawsef.Api.Services;

/// <summary>
/// Writes a PostgreSQL dump when the API starts (if a backup folder is set), then on the configured interval.
/// Uses the server clock via the backup service's <see cref="TimeProvider"/>. Failures are recorded as system errors.
/// </summary>
public class ScheduledBackupWorker(
    IServiceScopeFactory scopes,
    IOptions<ScheduledBackupOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromDays(Math.Max(1, options.Value.IntervalDays));
        using var timer = new PeriodicTimer(interval);
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await scope.ServiceProvider.GetRequiredService<ISystemErrorLogger>()
                    .LogAsync(ex, details: new { Job = nameof(ScheduledBackupWorker) }, cancellationToken: stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
