using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Options;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;

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
public record ScheduledBackupResult(string FilePath, int DeletedOldFiles);

public interface IScheduledBackupService
{
    /// <summary>
    /// Writes a dump into <c>SystemSettings.BackupPath</c> when that folder is set, creates the folder if needed,
    /// then deletes older dumps past the keep count. No-op when the path is unset.
    /// </summary>
    Task<ScheduledBackupResult?> RunAsync(CancellationToken cancellationToken);
}

public class ScheduledBackupService(
    ISystemSettingsCache settings,
    IDatabaseDumpRunner dump,
    IActivityLogger activity,
    IOptions<ScheduledBackupOptions> options,
    TimeProvider time) : IScheduledBackupService
{
    public const string FilePrefix = "mostawsef-";
    public const string FileExtension = ".dump";

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

        var deleted = PurgeOldDumps(folder, options.Value.KeepCount);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.BackupScheduled, ActivityModules.Backups, "Wrote a scheduled database dump")
            {
                Details = new { FilePath = filePath, DeletedOldFiles = deleted, options.Value.KeepCount },
            },
            cancellationToken);
        await activity.FlushAsync(cancellationToken);

        return new ScheduledBackupResult(filePath, deleted);
    }

    /// <summary>Keeps the newest <paramref name="keepCount"/> dump files; deletes the rest. Returns how many were deleted.</summary>
    internal static int PurgeOldDumps(string folder, int keepCount)
    {
        var files = Directory.EnumerateFiles(folder, FilePrefix + "*" + FileExtension)
            .Select(path => new FileInfo(path))
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
}
