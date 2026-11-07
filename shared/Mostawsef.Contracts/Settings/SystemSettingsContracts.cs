using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Settings;

public static class SystemSettingsRules
{
    public const int BackupPathMaxLength = 260;
}

public record SystemSettingsDto(
    string? BackupPath,
    bool MaintenanceMode,
    int Version,
    DateTime? UpdatedAt,
    string? UpdatedBy);

/// <summary>Version is the value from SystemSettingsDto; a stale one returns 409 concurrency_conflict.</summary>
public record UpdateSystemSettingsRequest
{
    [MaxLength(SystemSettingsRules.BackupPathMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? BackupPath { get; init; }

    public bool MaintenanceMode { get; init; }

    public int Version { get; init; }
}
