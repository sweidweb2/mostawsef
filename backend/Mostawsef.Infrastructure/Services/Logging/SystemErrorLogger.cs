using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Services.Logging;

/// <summary>Writes errors through its own DbContext (the request's context may be the thing that failed). Never throws.</summary>
public class SystemErrorLogger(
    IDbContextFactory<AppDbContext> dbFactory,
    ICurrentUser currentUser,
    ILogger<SystemErrorLogger> logger) : ISystemErrorLogger
{
    public const string ApiSource = "Api";

    public Task LogAsync(
        Exception exception,
        ErrorLevel level = ErrorLevel.Error,
        int? statusCode = null,
        object? details = null,
        CancellationToken cancellationToken = default) =>
        LogAsync(
            new SystemErrorEntry(level, ApiSource, exception.Message)
            {
                ExceptionType = exception.GetType().FullName,
                StackTrace = exception.ToString(),
                StatusCode = statusCode,
                Details = details,
            },
            cancellationToken);

    public async Task LogAsync(SystemErrorEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.SystemErrors.Add(new SystemError(
                entry.Level,
                entry.Source,
                entry.Message,
                entry.ExceptionType,
                entry.StackTrace,
                currentUser.RequestPath,
                currentUser.RequestMethod,
                entry.StatusCode,
                entry.UserId ?? currentUser.UserId,
                entry.Username ?? currentUser.Username,
                currentUser.TraceId,
                currentUser.IpAddress,
                entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, ActivityLogger.Json)));

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not write system error: {Message}", entry.Message);
        }
    }
}
