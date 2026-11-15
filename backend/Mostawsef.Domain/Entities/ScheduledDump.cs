using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>
/// One successful scheduled <c>pg_dump</c> run. The dump file stays on disk; this row records when, where, and which settings were used.
/// </summary>
public class ScheduledDump : Entity
{
    public const int FolderPathMaxLength = 260;
    public const int FilePathMaxLength = 400;

    private ScheduledDump()
    {
    }

    public ScheduledDump(
        string folderPath,
        string filePath,
        long fileSizeBytes,
        int keepCount,
        int intervalDays,
        int deletedOldFiles)
    {
        FolderPath = folderPath.Trim();
        FilePath = filePath.Trim();
        FileSizeBytes = fileSizeBytes < 0 ? 0 : fileSizeBytes;
        KeepCount = keepCount;
        IntervalDays = intervalDays;
        DeletedOldFiles = deletedOldFiles < 0 ? 0 : deletedOldFiles;
    }

    /// <summary>The Super settings backup folder used for this run.</summary>
    public string FolderPath { get; private set; } = string.Empty;

    /// <summary>Full path of the dump file written on disk.</summary>
    public string FilePath { get; private set; } = string.Empty;

    public long FileSizeBytes { get; private set; }

    /// <summary>How many newest dump files the retention setting kept after this run.</summary>
    public int KeepCount { get; private set; }

    /// <summary>Configured days between automatic dumps at the time of this run.</summary>
    public int IntervalDays { get; private set; }

    /// <summary>How many older dump files were deleted during retention for this run.</summary>
    public int DeletedOldFiles { get; private set; }
}
