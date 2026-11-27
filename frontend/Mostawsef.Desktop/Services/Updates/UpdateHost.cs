using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>Process / path hooks for applying a downloaded Setup (real app or fakes in tests).</summary>
public interface IUpdateHost
{
    string DownloadDirectory { get; }

    /// <summary>Starts the second process that re-checks <paramref name="sha256"/> and runs the Setup.</summary>
    void StartApplyUpdate(string setupPath, string sha256, int currentProcessId);

    void ExitApplication();
}

public sealed class UpdateHost : IUpdateHost
{
    /// <summary>This Desktop's exe: started again as the update helper, and relaunched after Setup.</summary>
    public static string DesktopExePath { get; } =
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "Mostawsef.Desktop.exe");

    public string DownloadDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mostawsef",
        "updates");

    public void StartApplyUpdate(string setupPath, string sha256, int currentProcessId) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = DesktopExePath,
            Arguments = UpdateApplicator.BuildArgs(setupPath, sha256, currentProcessId),
            UseShellExecute = true,
        });

    public void ExitApplication()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        });
    }
}
