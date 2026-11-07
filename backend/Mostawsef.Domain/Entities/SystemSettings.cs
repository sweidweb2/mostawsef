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
        BackupPath = string.IsNullOrWhiteSpace(backupPath) ? null : backupPath.Trim();
        MaintenanceMode = maintenanceMode;
    }
}
