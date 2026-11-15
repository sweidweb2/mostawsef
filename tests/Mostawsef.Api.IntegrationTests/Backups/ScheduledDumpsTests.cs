using System.Net;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Mostawsef.Api.IntegrationTests.Backups;

[Collection(ApiCollection.Name)]
public class ScheduledDumpsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_successful_dump_appears_in_the_history()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mostawsef-dump-history", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await factory.UpdateSystemSettingsAsync(
            current => new UpdateSystemSettingsRequest { BackupPath = folder, Version = current.Version },
            Ct);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunAsync(Ct);
            Assert.NotNull(result);
        }

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<PagedResult<ScheduledDumpDto>>("api/scheduled-dumps?page=1&pageSize=20&includeTotal=true");

        Assert.Contains(page.Items, d =>
            d.FolderPath == folder
            && d.FilePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
            && d.FileName.StartsWith(ScheduledBackupService.FilePrefix, StringComparison.Ordinal)
            && d.KeepCount >= 1
            && d.IntervalDays >= 1
            && d.FileSizeBytes > 0);
    }

    [Fact]
    public async Task Admin_cannot_list_scheduled_dumps()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("api/scheduled-dumps", Ct)).StatusCode);
    }

    [Fact]
    public async Task Signed_out_users_get_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/scheduled-dumps", Ct)).StatusCode);
    }
}
