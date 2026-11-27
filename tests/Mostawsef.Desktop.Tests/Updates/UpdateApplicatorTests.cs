using System.Diagnostics;
using System.Security.Cryptography;
using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Updates;

public class UpdateApplicatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mostawsef-update-lock-tests", Guid.NewGuid().ToString("N"));
    private readonly UpdateApplicator _applicator;

    public UpdateApplicatorTests()
    {
        Directory.CreateDirectory(_dir);
        _applicator = new UpdateApplicator(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp folder.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsUpdateLockHeld_is_false_when_no_lock_file()
    {
        Assert.False(_applicator.IsUpdateLockHeld());
    }

    [Fact]
    public void IsUpdateLockHeld_is_true_while_another_process_holds_the_file()
    {
        using var held = new FileStream(
            _applicator.LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.True(_applicator.IsUpdateLockHeld());
    }

    [Fact]
    public async Task WaitForUpdateLock_returns_when_the_lock_is_released()
    {
        var held = new FileStream(
            _applicator.LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        var waiter = _applicator.WaitForUpdateLockAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await held.DisposeAsync();

        Assert.True(await waiter);
        Assert.False(_applicator.IsUpdateLockHeld());
    }

    [Fact]
    public async Task WaitForUpdateLock_gives_up_after_the_timeout()
    {
        await using var held = new FileStream(
            _applicator.LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.False(await _applicator.WaitForUpdateLockAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_command_line_the_app_builds_is_the_one_the_helper_reads()
    {
        var args = UpdateApplicator.BuildArgs(@"C:\Users\Clinic PC\updates\setup.exe", "abc123", 42);

        Assert.True(UpdateApplicator.TryParseArgs(
            ["Mostawsef.Desktop.exe", .. SplitLikeWindows(args)], out var path, out var sha256, out var pid));
        Assert.Equal(@"C:\Users\Clinic PC\updates\setup.exe", path);
        Assert.Equal("abc123", sha256);
        Assert.Equal(42, pid);
    }

    [Fact]
    public void TryParseArgs_requires_setup_path()
    {
        Assert.False(UpdateApplicator.TryParseArgs(["--apply-update"], out _, out _, out _));
        Assert.False(UpdateApplicator.TryParseArgs(["--other"], out _, out _, out _));
    }

    [Fact]
    public void TryParseArgs_refuses_a_setup_without_its_hash()
    {
        Assert.False(UpdateApplicator.TryParseArgs(
            ["--apply-update", @"C:\Temp\setup.exe", "--wait-pid", "1234"], out _, out _, out _));
    }

    [Fact]
    public void A_setup_that_changed_after_download_is_deleted_and_never_started()
    {
        var setup = WriteSetup("original");
        var expected = Convert.ToHexString(SHA256.HashData("original"u8.ToArray()));
        File.WriteAllText(setup, "swapped");
        var started = false;

        var code = _applicator.Apply(setup, expected, MissingExe(), startElevated: _ =>
        {
            started = true;
            return null;
        });

        Assert.Equal(4, code);
        Assert.False(started);
        Assert.False(File.Exists(setup));
    }

    [Fact]
    public void A_matching_setup_starts_and_cannot_be_replaced_while_it_runs()
    {
        var setup = WriteSetup("payload");
        var expected = Convert.ToHexString(SHA256.HashData("payload"u8.ToArray()));
        Exception? writeError = null;

        var code = _applicator.Apply(setup, expected.ToLowerInvariant(), MissingExe(), startElevated: path =>
        {
            writeError = Record.Exception(() => File.WriteAllText(path, "swapped"));
            return (Process?)null;
        });

        Assert.Equal(3, code); // the fake starter returns no process
        Assert.IsType<IOException>(writeError);
        Assert.Equal("payload", File.ReadAllText(setup));
        Assert.False(_applicator.IsUpdateLockHeld());
    }

    /// <summary>Splits on spaces outside double quotes and drops the quotes, as the Windows command line does.</summary>
    private static string[] SplitLikeWindows(string commandLine)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (c == ' ' && !quoted)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        parts.Add(current.ToString());
        return [.. parts];
    }

    private string WriteSetup(string content)
    {
        var path = Path.Combine(_dir, "Mostawsef-Setup-99.0.0.exe");
        File.WriteAllText(path, content);
        return path;
    }

    private string MissingExe() => Path.Combine(_dir, "not-installed", "Mostawsef.Desktop.exe");
}
