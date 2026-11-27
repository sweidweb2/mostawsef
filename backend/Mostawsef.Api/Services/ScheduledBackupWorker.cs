using Microsoft.Extensions.Options;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;

namespace Mostawsef.Api.Services;

/// <summary>
/// Checks once at start and then every hour whether a PostgreSQL dump is due (see
/// <see cref="IScheduledBackupService.RunIfDueAsync"/>), so the interval counts from the last dump, not from each
/// restart. A failure is recorded as a system error and the next attempt waits one interval.
/// </summary>
public class ScheduledBackupWorker(
    IServiceScopeFactory scopes,
    IOptions<ScheduledBackupOptions> options,
    TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = TimeSpan.FromDays(Math.Max(1, options.Value.IntervalDays));
        var pausedUntil = DateTimeOffset.MinValue;
        using var timer = new PeriodicTimer(CheckInterval, time);
        do
        {
            if (time.GetUtcNow() < pausedUntil)
            {
                continue;
            }

            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunIfDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                pausedUntil = time.GetUtcNow() + retryDelay;
                await scope.ServiceProvider.GetRequiredService<ISystemErrorLogger>()
                    .LogAsync(ex, details: new { Job = nameof(ScheduledBackupWorker) }, cancellationToken: stoppingToken);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
