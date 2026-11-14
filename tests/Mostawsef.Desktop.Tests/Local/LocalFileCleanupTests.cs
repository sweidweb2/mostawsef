using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Tests.Local;

public sealed class LocalFileCleanupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MostawsefCleanupTests", Guid.NewGuid().ToString("N"));

    public LocalFileCleanupTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Create(string name, int ageDays)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTime(path, DateTime.Now.AddDays(-ageDays));
        return path;
    }

    [Fact]
    public void Deletes_only_matching_files_older_than_the_limit()
    {
        var oldLog = Create("errors-old.log", 40);
        var newLog = Create("errors-new.log", 2);
        var oldOther = Create("keep-me.txt", 400);

        var deleted = LocalFileCleanup.DeleteOlderThan(_dir, "*.log", LocalFileCleanup.LogRetention);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(newLog));
        Assert.True(File.Exists(oldOther));
    }

    [Fact]
    public void A_missing_folder_is_not_an_error()
    {
        Assert.Equal(0, LocalFileCleanup.DeleteOlderThan(Path.Combine(_dir, "nope"), "*.log", TimeSpan.FromDays(1)));
    }

    [Fact]
    public void A_file_that_is_still_open_is_skipped()
    {
        var path = Create("busy.log", 40);
        using var open = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var deleted = LocalFileCleanup.DeleteOlderThan(_dir, "*.log", TimeSpan.FromDays(1));

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Run_sweeps_old_logs()
    {
        var oldLog = Create("perf-old.log", 31);
        var newLog = Create("perf-new.log", 1);

        LocalFileCleanup.Run(_dir);

        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(newLog));
    }
}
