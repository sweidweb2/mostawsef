using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Services.Logging;

/// <summary>
/// Writes activities through its own DbContext, so a failing business transaction cannot block or roll back the log,
/// and a logging failure never breaks the caller.
/// </summary>
public class ActivityLogger(
    IDbContextFactory<AppDbContext> dbFactory,
    ICurrentUser currentUser,
    ILogger<ActivityLogger> logger) : IActivityLogger
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task LogAsync(ActivityEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.ActivityLogs.Add(new ActivityLog(
                entry.Action,
                entry.Module,
                entry.Description,
                entry.UserId ?? currentUser.UserId,
                entry.Username ?? currentUser.Username,
                entry.EntityType,
                entry.EntityId,
                currentUser.IpAddress,
                currentUser.UserAgent,
                entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, Json)));

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not write activity log {Action}", entry.Action);
        }
    }
}
