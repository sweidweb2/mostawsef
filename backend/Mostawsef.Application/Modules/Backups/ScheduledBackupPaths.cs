namespace Mostawsef.Application.Modules.Backups;

/// <summary>Default folder for scheduled dumps: <c>Backups</c> next to the API install (BaseDirectory).</summary>
public static class ScheduledBackupPaths
{
    public const string FolderName = "Backups";

    /// <summary>Full path under the running API's install folder. Super can change it in Settings.</summary>
    public static string DefaultFolder { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, FolderName));
}
