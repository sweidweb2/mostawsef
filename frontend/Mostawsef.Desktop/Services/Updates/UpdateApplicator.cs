using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>
/// The second process of an in-app update (<c>Mostawsef.Desktop.exe --apply-update ...</c>): waits for the old Desktop
/// to exit, re-checks the Setup's hash, runs it elevated and silent, then relaunches Desktop. While it runs it holds
/// an update lock file, so a Desktop opened mid-update waits instead of fighting Setup over locked files.
/// Owns its command line: <see cref="BuildArgs"/> writes it, <see cref="TryParseArgs"/> reads it.
/// </summary>
public sealed class UpdateApplicator(string lockDirectory)
{
    public const string ApplyUpdateArg = "--apply-update";
    public const string WaitPidArg = "--wait-pid";
    public const string Sha256Arg = "--sha256";
    public const string SilentSetupArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    private static readonly TimeSpan LockPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Production lock folder: <c>%LocalAppData%\Mostawsef</c>.</summary>
    public static string DefaultLockDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mostawsef");

    public string LockFilePath { get; } = Path.Combine(lockDirectory, "update.lock");

    /// <summary>The arguments that start this helper for <paramref name="setupPath"/> (read back by <see cref="TryParseArgs"/>).</summary>
    public static string BuildArgs(string setupPath, string sha256, int waitPid) =>
        $"{ApplyUpdateArg} \"{setupPath}\" {Sha256Arg} {sha256} {WaitPidArg} {waitPid}";

    public static bool TryParseArgs(string[] args, out string setupPath, out string sha256, out int? waitPid)
    {
        setupPath = string.Empty;
        sha256 = string.Empty;
        waitPid = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], ApplyUpdateArg, StringComparison.OrdinalIgnoreCase))
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
                if (string.Equals(args[j], WaitPidArg, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[j + 1], out var pid))
                {
                    waitPid = pid;
                }
                else if (string.Equals(args[j], Sha256Arg, StringComparison.OrdinalIgnoreCase))
                {
                    sha256 = args[j + 1];
                }
            }

            // Without the expected hash the Setup is never run (it would run elevated unchecked).
            return !string.IsNullOrWhiteSpace(setupPath) && !string.IsNullOrWhiteSpace(sha256);
        }

        return false;
    }

    /// <summary>Case-insensitive compare of a SHA-256 digest with the feed's hex string.</summary>
    public static bool HashMatches(byte[] hash, string expectedHex) =>
        string.Equals(Convert.ToHexString(hash), expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Starts Desktop again (after Setup, or after waiting out another instance's update). No-op when it is not installed.</summary>
    public static void RelaunchDesktop(string desktopExePath)
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

    public bool IsUpdateLockHeld()
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

    /// <summary>Waits until the update lock is released (true) or <paramref name="timeout"/> passes (false).</summary>
    public async Task<bool> WaitForUpdateLockAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < timeout)
        {
            if (!IsUpdateLockHeld())
            {
                TryDeleteLock();
                return true;
            }

            await Task.Delay(LockPollInterval, cancellationToken).ConfigureAwait(false);
        }

        return !IsUpdateLockHeld();
    }

    /// <summary>
    /// Runs elevated Setup then relaunches Desktop. Returns process exit code to the OS
    /// (0 = Setup accepted / finished with 0; non-zero = Setup failed; -1 = UAC cancel still relaunches Desktop;
    /// 4 = the file no longer matches <paramref name="expectedSha256"/>, Setup not started).
    /// The file is re-hashed here and kept open (no writers or deletes allowed) until Setup exits, because the
    /// download folder is writable by the user: a file swapped after the first check must never run elevated.
    /// </summary>
    public int Apply(
        string setupPath,
        string expectedSha256,
        string desktopExePath,
        int? waitPid = null,
        Func<string, Process?>? startElevated = null)
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

        Directory.CreateDirectory(lockDirectory);
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

            using var pinned = new FileStream(setupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!HashMatches(SHA256.HashData(pinned), expectedSha256))
            {
                pinned.Dispose();
                TryDelete(setupPath);
                RelaunchDesktop(desktopExePath);
                return 4;
            }

            try
            {
                var setup = (startElevated ?? StartSetupElevated)(setupPath);

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

    private static Process? StartSetupElevated(string setupPath) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = SilentSetupArguments,
            UseShellExecute = true,
            Verb = "runas",
        });

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Still open elsewhere: the next download replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the next download replaces it.
        }
    }

    private void TryDeleteLock()
    {
        try
        {
            File.Delete(LockFilePath);
        }
        catch (IOException)
        {
            // Another instance just took the lock: it deletes the file when it is done.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the next start waits briefly, then continues.
        }
    }
}
