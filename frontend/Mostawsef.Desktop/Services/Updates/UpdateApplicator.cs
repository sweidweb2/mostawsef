using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>
/// Second-process helper: wait for the old Desktop to exit, run Setup elevated+silent, relaunch Desktop.
/// Also exposes a lock so a mid-update reopen can wait instead of fighting file locks.
/// </summary>
public static class UpdateApplicator
{
    public static readonly string DefaultLockDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mostawsef");

    /// <summary>Override in tests; production uses <see cref="DefaultLockDirectory"/>.</summary>
    public static string LockDirectory { get; set; } = DefaultLockDirectory;

    public static string LockFilePath => Path.Combine(LockDirectory, "update.lock");

    public static bool TryParseArgs(string[] args, out string setupPath, out int? waitPid)
    {
        setupPath = string.Empty;
        waitPid = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], UpdateHost.ApplyUpdateArg, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return false;
            }

            setupPath = args[i + 1].Trim('"');
            for (var j = i + 2; j < args.Length - 1; j++)
            {
                if (string.Equals(args[j], UpdateHost.WaitPidArg, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[j + 1], out var pid))
                {
                    waitPid = pid;
                }
            }

            return !string.IsNullOrWhiteSpace(setupPath);
        }

        return false;
    }

    public static bool IsUpdateLockHeld()
    {
        if (!File.Exists(LockFilePath))
        {
            return false;
        }

        try
        {
            using var _ = new FileStream(LockFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>Blocks until the update lock is released (or timeout). Returns false on timeout.</summary>
    public static bool WaitForUpdateLock(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsUpdateLockHeld())
            {
                TryDeleteLock();
                return true;
            }

            Thread.Sleep(500);
        }

        return !IsUpdateLockHeld();
    }

    /// <summary>
    /// Runs elevated Setup then relaunches Desktop. Returns process exit code to the OS
    /// (0 = Setup accepted / finished with 0; non-zero = Setup failed; -1 = UAC cancel still relaunches Desktop).
    /// </summary>
    public static int Apply(string setupPath, string desktopExePath, int? waitPid = null)
    {
        if (waitPid is int pid)
        {
            try
            {
                using var parent = Process.GetProcessById(pid);
                parent.WaitForExit(120_000);
            }
            catch (ArgumentException)
            {
                // Already exited.
            }
        }

        Directory.CreateDirectory(LockDirectory);
        using var lockStream = new FileStream(
            LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        try
        {
            if (!File.Exists(setupPath))
            {
                RelaunchDesktop(desktopExePath);
                return 2;
            }

            try
            {
                var setup = Process.Start(new ProcessStartInfo
                {
                    FileName = setupPath,
                    Arguments = UpdateHost.SilentSetupArguments,
                    UseShellExecute = true,
                    Verb = "runas",
                });

                if (setup is null)
                {
                    RelaunchDesktop(desktopExePath);
                    return 3;
                }

                setup.WaitForExit();
                var code = setup.ExitCode;
                RelaunchDesktop(desktopExePath);
                return code;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // UAC cancelled — keep the old app usable.
                RelaunchDesktop(desktopExePath);
                return -1;
            }
        }
        finally
        {
            lockStream.Dispose();
            TryDeleteLock();
        }
    }

    private static void RelaunchDesktop(string desktopExePath)
    {
        if (!File.Exists(desktopExePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = desktopExePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(desktopExePath) ?? AppContext.BaseDirectory,
        });
    }

    private static void TryDeleteLock()
    {
        try
        {
            if (File.Exists(LockFilePath))
            {
                File.Delete(LockFilePath);
            }
        }
        catch
        {
            // Best-effort; next start may wait briefly then continue.
        }
    }
}
