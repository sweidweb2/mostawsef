using Mostawsef.Application.Modules.Backups;

namespace Mostawsef.Api.IntegrationTests.Orders;

public class DumpFileNameTests
{
    [Theory]
    [InlineData("mostawsef-20261127-123456.dump", true)]
    [InlineData("mostawsef-notes.dump", false)]
    [InlineData("other-20261127-123456.dump", false)]
    [InlineData("mostawsef-20261127-123456.dump.bak", false)]
    public void Only_stamped_dump_names_are_owned(string name, bool owned) =>
        Assert.Equal(owned, ScheduledBackupService.IsOwnDumpFileName(name));

    [Fact]
    public void Purge_skips_files_that_are_not_owned_dumps()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mostawsef-dump-purge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var keep = Path.Combine(folder, "mostawsef-20261127-120000.dump");
            var old = Path.Combine(folder, "mostawsef-20261120-120000.dump");
            var foreign = Path.Combine(folder, "mostawsef-notes.dump");
            File.WriteAllText(keep, "keep");
            File.WriteAllText(old, "old");
            File.WriteAllText(foreign, "foreign");
            File.SetLastWriteTimeUtc(keep, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-7));
            File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddDays(-8));

            Assert.Equal(1, ScheduledBackupService.PurgeOldDumps(folder, keepCount: 1));
            Assert.True(File.Exists(keep));
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(foreign));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
