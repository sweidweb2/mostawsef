using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Options;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Backups;

/// <summary>Bound from the "ScheduledBackup" configuration section.</summary>
public class ScheduledBackupOptions
{
    public const string SectionName = "ScheduledBackup";

    /// <summary>Days between automatic dumps while the API is running. Minimum 1.</summary>
    [Range(1, 365)]
    public int IntervalDays { get; set; } = 1;

    /// <summary>How many <c>mostawsef-*.dump</c> files to keep in the backup folder. Oldest are deleted.</summary>
    [Range(1, 100)]
    public int KeepCount { get; set; } = 7;

    /// <summary>Optional full path to <c>pg_dump</c>; empty searches PATH and common install folders.</summary>
    public string? PgDumpPath { get; set; }
}

/// <summary>Outcome of one scheduled-dump attempt. Null when skipped (no backup folder set).</summary>
public record ScheduledBackupResult(string FilePath, int DeletedOldFiles, Guid HistoryId);

public interface IScheduledBackupService
{
    /// <summary>
    /// Writes a dump into <c>SystemSettings.BackupPath</c> when that folder is set, creates the folder if needed,
    /// then deletes older dumps past the keep count and stores a history row. No-op when the path is unset.
    /// </summary>
    Task<ScheduledBackupResult?> RunAsync(CancellationToken cancellationToken);

    /// <summary>History of successful scheduled dumps, newest first (Super list page).</summary>
    Task<PagedResult<ScheduledDumpDto>> GetHistoryAsync(PageQuery query, CancellationToken cancellationToken);
}

public class ScheduledBackupService(
    ISystemSettingsCache settings,
    IDatabaseDumpRunner dump,
    IScheduledDumpRepository history,
    IActivityLogger activity,
    IOptions<ScheduledBackupOptions> options,
    TimeProvider time) : IScheduledBackupService
{
    public const string FilePrefix = "mostawsef-";
    public const string FileExtension = ".dump";

    public async Task<PagedResult<ScheduledDumpDto>> GetHistoryAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await history.GetPageAsync(query, cancellationToken);
        return new PagedResult<ScheduledDumpDto>(items, query.SafePage, query.SafePageSize, total);
    }

    public async Task<ScheduledBackupResult?> RunAsync(CancellationToken cancellationToken)
    {
        var folder = (await settings.GetAsync(cancellationToken)).BackupPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        Directory.CreateDirectory(folder);

        var stamp = time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(folder, FilePrefix + stamp + FileExtension);
        await dump.RunAsync(filePath, cancellationToken);

        var keepCount = options.Value.KeepCount;
        var intervalDays = options.Value.IntervalDays;
        var deleted = PurgeOldDumps(folder, keepCount);
        var size = new FileInfo(filePath).Length;

        var record = new ScheduledDump(folder, filePath, size, keepCount, intervalDays, deleted);
        history.Add(record);
        await history.SaveChangesAsync(cancellationToken);
        await history.TrimToAsync(keepCount, cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.BackupScheduled, ActivityModules.Backups, "Wrote a scheduled database dump")
            {
                EntityType = nameof(ScheduledDump),
                EntityId = record.Id.ToString(),
                Details = new { FilePath = filePath, DeletedOldFiles = deleted, KeepCount = keepCount, IntervalDays = intervalDays },
            },
            cancellationToken);
        await activity.FlushAsync(cancellationToken);

        return new ScheduledBackupResult(filePath, deleted, record.Id);
    }

    /// <summary>
    /// Keeps the newest <paramref name="keepCount"/> dump files whose names match the stamp pattern
    /// (<c>mostawsef-yyyyMMdd-HHmmss.dump</c>); deletes only those. Returns how many were deleted.
    /// </summary>
    public static int PurgeOldDumps(string folder, int keepCount)
    {
        var files = Directory.EnumerateFiles(folder, FilePrefix + "*" + FileExtension)
            .Select(path => new FileInfo(path))
            .Where(f => IsOwnDumpFileName(f.Name))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .ToList();

        var deleted = 0;
        foreach (var old in files.Skip(keepCount))
        {
            old.Delete();
            deleted++;
        }

        return deleted;
    }

    /// <summary>True for <c>mostawsef-yyyyMMdd-HHmmss.dump</c> only — refuses to touch other files in the folder.</summary>
    public static bool IsOwnDumpFileName(string fileName)
    {
        const string prefix = FilePrefix;
        const string extension = FileExtension;
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(extension, StringComparison.Ordinal))
        {
            return false;
        }

        var stamp = fileName.AsSpan(prefix.Length, fileName.Length - prefix.Length - extension.Length);
        // yyyyMMdd-HHmmss = 15 characters with a dash at index 8.
        return stamp.Length == 15
            && stamp[8] == '-'
            && char.IsDigit(stamp[0]) && char.IsDigit(stamp[1]) && char.IsDigit(stamp[2]) && char.IsDigit(stamp[3])
            && char.IsDigit(stamp[4]) && char.IsDigit(stamp[5]) && char.IsDigit(stamp[6]) && char.IsDigit(stamp[7])
            && char.IsDigit(stamp[9]) && char.IsDigit(stamp[10]) && char.IsDigit(stamp[11])
            && char.IsDigit(stamp[12]) && char.IsDigit(stamp[13]) && char.IsDigit(stamp[14]);
    }
}
