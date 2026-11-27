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
    string DesktopExePath { get; }

    string DownloadDirectory { get; }

    /// <summary>Default <c>C:\Mostawsef\install-clinic.log</c> next to the install root.</summary>
    string InstallClinicLogPath { get; }

    void StartApplyUpdate(string setupPath, int currentProcessId);

    void ExitApplication();
}

public sealed class UpdateHost : IUpdateHost
{
    public const string ApplyUpdateArg = "--apply-update";
    public const string WaitPidArg = "--wait-pid";

    public static readonly string SilentSetupArguments =
        "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    public string DesktopExePath { get; } =
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "Mostawsef.Desktop.exe");

    public string DownloadDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mostawsef",
        "updates");

    public string InstallClinicLogPath { get; } = ResolveInstallClinicLogPath();

    public void StartApplyUpdate(string setupPath, int currentProcessId)
    {
        var args = $"{ApplyUpdateArg} \"{setupPath}\" {WaitPidArg} {currentProcessId}";
        Process.Start(new ProcessStartInfo
        {
            FileName = DesktopExePath,
            Arguments = args,
            UseShellExecute = true,
        });
    }

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

    private static string ResolveInstallClinicLogPath()
    {
        // Published layout: C:\Mostawsef\Desktop\*.exe → log at C:\Mostawsef\install-clinic.log
        var installRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        return Path.Combine(installRoot, "install-clinic.log");
    }
}
