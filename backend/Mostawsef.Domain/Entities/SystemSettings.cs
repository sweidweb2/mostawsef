using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>System-wide options managed by Super. Exactly one row, created at startup.</summary>
public class SystemSettings : Entity
{
    public const int BackupPathMaxLength = 260;

    /// <summary>Folder where scheduled backups are written; null until Super sets it.</summary>
    public string? BackupPath { get; private set; }

    /// <summary>While on, only Super users can sign in; existing sessions keep working.</summary>
    public bool MaintenanceMode { get; private set; }

    public void Update(string? backupPath, bool maintenanceMode)
    {
        BackupPath = NormalizeBackupPath(backupPath);
        MaintenanceMode = maintenanceMode;
    }

    /// <summary>
    /// Empty clears the folder. Otherwise the path must be absolute and must not contain ".." segments,
    /// so scheduled dumps cannot purge files outside a Super-chosen rooted folder.
    /// </summary>
    public static string? NormalizeBackupPath(string? backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return null;
        }

        var trimmed = backupPath.Trim();
        if (trimmed.Length > BackupPathMaxLength
            || trimmed.Contains("..", StringComparison.Ordinal)
            || !Path.IsPathRooted(trimmed))
        {
            throw new ArgumentException("Backup path must be an absolute folder path without '..'.", nameof(backupPath));
        }

        return Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
