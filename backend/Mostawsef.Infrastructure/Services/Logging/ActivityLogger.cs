using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;
using Mostawsef.Infrastructure.Persistence.Repositories;

namespace Mostawsef.Infrastructure.Services.Logging;

/// <summary>
/// Buffers activities for the current request and writes them once on <see cref="FlushAsync"/> (after the action,
/// before the HTTP response finishes), so a failing business transaction cannot roll them back, a logging failure
/// never breaks the caller, and several logs in one request share a single insert.
/// </summary>
public sealed class ActivityLogger(
    IDbContextFactory<AppDbContext> dbFactory,
    ICurrentUser currentUser,
    TimeProvider time,
    ILogger<ActivityLogger> logger) : IActivityLogger
{
    private List<ActivityLog> _pending = [];

    public Task LogAsync(ActivityEntry entry, CancellationToken cancellationToken = default)
    {
        var log = new ActivityLog(
            time.GetUtcNow().UtcDateTime,
            entry.Action,
            entry.Module,
            entry.Description,
            entry.UserId ?? currentUser.UserId,
            entry.Username ?? currentUser.Username,
            entry.EntityType,
            entry.EntityId,
            currentUser.IpAddress,
            currentUser.UserAgent,
            entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, LogJson.Options));
        log.FillSearchText();
        _pending.Add(log);
        return Task.CompletedTask;
    }

    /// <summary>Writes every buffered entry in one SaveChanges. Safe to call more than once.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        var batch = _pending;
        _pending = [];

        try
        {
            await WriteBatchAsync(batch, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Retrying {Count} activity log(s) after a failed flush", batch.Count);
            try
            {
                await WriteBatchAsync(batch, cancellationToken);
            }
            catch (Exception retry) when (retry is not OperationCanceledException)
            {
                logger.LogError(retry, "Could not write {Count} activity log(s)", batch.Count);
            }
        }
    }

    private async Task WriteBatchAsync(List<ActivityLog> batch, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.ActivityLogs.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
    }
}
