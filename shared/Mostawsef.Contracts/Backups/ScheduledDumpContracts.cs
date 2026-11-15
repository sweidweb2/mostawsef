namespace Mostawsef.Contracts.Backups;

/// <summary>One successful scheduled database dump (history only; the file is on disk).</summary>
public record ScheduledDumpDto(
    Guid Id,
    string FolderPath,
    string FilePath,
    long FileSizeBytes,
    int KeepCount,
    int IntervalDays,
    int DeletedOldFiles,
    DateTime CreatedAt)
{
    /// <summary>File name only (for the list column).</summary>
    public string FileName
    {
        get
        {
            var path = FilePath;
            var slash = path.LastIndexOf('\\');
            var forward = path.LastIndexOf('/');
            var i = slash > forward ? slash : forward;
            return i >= 0 ? path[(i + 1)..] : path;
        }
    }
}
