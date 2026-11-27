using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Updates;

public class UpdateApplicatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mostawsef-update-lock-tests", Guid.NewGuid().ToString("N"));
    private readonly string _previousLockDir;

    public UpdateApplicatorTests()
    {
        Directory.CreateDirectory(_dir);
        _previousLockDir = UpdateApplicator.LockDirectory;
        UpdateApplicator.LockDirectory = _dir;
    }

    public void Dispose()
    {
        UpdateApplicator.LockDirectory = _previousLockDir;
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsUpdateLockHeld_is_false_when_no_lock_file()
    {
        Assert.False(UpdateApplicator.IsUpdateLockHeld());
    }

    [Fact]
    public void IsUpdateLockHeld_is_true_while_another_process_holds_the_file()
    {
        using var held = new FileStream(
            UpdateApplicator.LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.True(UpdateApplicator.IsUpdateLockHeld());
    }

    [Fact]
    public async Task WaitForUpdateLock_returns_when_the_lock_is_released()
    {
        var held = new FileStream(
            UpdateApplicator.LockFilePath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);

        var waiter = Task.Run(
            () => UpdateApplicator.WaitForUpdateLock(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await held.DisposeAsync();

        Assert.True(await waiter);
        Assert.False(UpdateApplicator.IsUpdateLockHeld());
    }

    [Fact]
    public void TryParseArgs_requires_setup_path()
    {
        Assert.False(UpdateApplicator.TryParseArgs(["--apply-update"], out _, out _));
        Assert.False(UpdateApplicator.TryParseArgs(["--other"], out _, out _));
    }
}
