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

    /// <summary>Callers check <see cref="TryNormalizeBackupPath"/> first; an invalid path here is a bug.</summary>
    public void Update(string? backupPath, bool maintenanceMode)
    {
        if (!TryNormalizeBackupPath(backupPath, out var normalized))
        {
            throw new ArgumentException("Backup path must be a full folder path without '..' segments.", nameof(backupPath));
        }

        BackupPath = normalized;
        MaintenanceMode = maintenanceMode;
    }

    /// <summary>
    /// Empty clears the folder (null). Otherwise the path must be fully qualified (e.g. <c>D:\Backups</c>, not
    /// <c>\Backups</c> or <c>Backups</c>) and must not have a ".." segment, so scheduled dumps cannot purge files
    /// outside a Super-chosen folder. Folder names that merely contain dots (<c>Backups..old</c>) are fine.
    /// </summary>
    public static bool TryNormalizeBackupPath(string? backupPath, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            return true;
        }

        var trimmed = backupPath.Trim();
        if (trimmed.Length > BackupPathMaxLength || !Path.IsPathFullyQualified(trimmed) || HasParentSegment(trimmed))
        {
            return false;
        }

        normalized = Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return true;
    }

    private static bool HasParentSegment(string path)
    {
        foreach (var segment in path.Split('\\', '/'))
        {
            if (segment == "..")
            {
                return true;
            }
        }

        return false;
    }
}
