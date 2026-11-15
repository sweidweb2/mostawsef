using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Globalization;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Api.IntegrationTests.Backups;

[Collection(ApiCollection.Name)]
public class ScheduledBackupTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_backup_folder_skips_the_dump()
    {
        using var host = CreateHost(keepCount: 3);
        await ClearBackupPathAsync(host);

        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunAsync(Ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_dump_is_written_to_the_configured_folder_and_audited()
    {
        var folder = NewTempFolder();
        using var host = CreateHost(keepCount: 7);
        await SetBackupPathAsync(host, folder);

        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunAsync(Ct);

        Assert.NotNull(result);
        Assert.True(File.Exists(result.FilePath));
        Assert.StartsWith(ScheduledBackupService.FilePrefix, Path.GetFileName(result.FilePath), StringComparison.Ordinal);
        Assert.Equal(ScheduledBackupService.FileExtension, Path.GetExtension(result.FilePath));
        Assert.Equal("fake-dump", await File.ReadAllTextAsync(result.FilePath, Ct));

        var super = await SuperClientAsync(host);
        var audit = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=backups.scheduled");
        Assert.Contains(audit.Items, i => i.Action == "backups.scheduled" && i.Module == "Backups");
    }

    [Fact]
    public async Task Old_dumps_past_the_keep_count_are_deleted()
    {
        var folder = NewTempFolder();
        using var host = CreateHost(keepCount: 2);
        await SetBackupPathAsync(host, folder);

        for (var i = 0; i < 3; i++)
        {
            var stale = Path.Combine(folder, $"{ScheduledBackupService.FilePrefix}old{i}{ScheduledBackupService.FileExtension}");
            await File.WriteAllTextAsync(stale, "old", Ct);
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-10 + i));
        }

        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IScheduledBackupService>().RunAsync(Ct);

        Assert.NotNull(result);
        Assert.Equal(2, result.DeletedOldFiles);
        var remaining = Directory.GetFiles(folder, ScheduledBackupService.FilePrefix + "*" + ScheduledBackupService.FileExtension);
        Assert.Equal(2, remaining.Length);
        Assert.Contains(result.FilePath, remaining);
    }

    [Fact]
    public void Api_does_not_start_with_an_invalid_keep_count()
    {
        using var badFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["ScheduledBackup:KeepCount"] = "0" })));

        var error = Record.Exception(() => badFactory.CreateClient());

        Assert.NotNull(error);
        Assert.True(error.ToString().Contains(nameof(ScheduledBackupOptions.KeepCount)), error.ToString());
    }

    private WebApplicationFactory<Program> CreateHost(int keepCount) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ScheduledBackup:KeepCount"] = keepCount.ToString(CultureInfo.InvariantCulture),
                    ["ScheduledBackup:IntervalDays"] = "1",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDatabaseDumpRunner>();
                services.AddSingleton<IDatabaseDumpRunner, FakeDumpRunner>();
            });
        });

    private async Task<HttpClient> SuperClientAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.WithBearer(await factory.GetAccessTokenAsync(TestUsers.Super));
        return client;
    }

    private async Task SetBackupPathAsync(WebApplicationFactory<Program> host, string folder)
    {
        var super = await SuperClientAsync(host);
        var current = await super.GetJsonAsync<SystemSettingsDto>("api/system-settings");
        var response = await super.PutAsJsonAsync(
            "api/system-settings",
            new UpdateSystemSettingsRequest { BackupPath = folder, Version = current.Version },
            Ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task ClearBackupPathAsync(WebApplicationFactory<Program> host)
    {
        var super = await SuperClientAsync(host);
        var current = await super.GetJsonAsync<SystemSettingsDto>("api/system-settings");
        var response = await super.PutAsJsonAsync(
            "api/system-settings",
            new UpdateSystemSettingsRequest { BackupPath = " ", Version = current.Version },
            Ct);
        response.EnsureSuccessStatusCode();
    }

    private static string NewTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mostawsef-scheduled-backup-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class FakeDumpRunner : IDatabaseDumpRunner
    {
        public Task RunAsync(string outputFilePath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
            return File.WriteAllTextAsync(outputFilePath, "fake-dump", cancellationToken);
        }
    }
}
