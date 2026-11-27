using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Api.Services;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Backups;
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
        Assert.NotEqual(Guid.Empty, result.HistoryId);

        var super = await SuperClientAsync(host);
        var audit = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=backups.scheduled");
        Assert.Contains(audit.Items, i => i.Action == "backups.scheduled" && i.Module == "Backups");

        var history = await super.GetJsonAsync<PagedResult<ScheduledDumpDto>>("api/scheduled-dumps?includeTotal=true");
        Assert.Contains(history.Items, d => d.Id == result.HistoryId && d.FilePath == result.FilePath);
    }

    [Fact]
    public async Task Old_dumps_past_the_keep_count_are_deleted()
    {
        var folder = NewTempFolder();
        using var host = CreateHost(keepCount: 2);
        await SetBackupPathAsync(host, folder);

        for (var i = 0; i < 3; i++)
        {
            // Stamp must match IsOwnDumpFileName (mostawsef-yyyyMMdd-HHmmss.dump).
            var stale = Path.Combine(folder, $"{ScheduledBackupService.FilePrefix}2020010{i}-120000{ScheduledBackupService.FileExtension}");
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
    public async Task History_rows_past_the_keep_count_are_deleted()
    {
        var folder = NewTempFolder();
        using var host = CreateHost(keepCount: 2);
        await SetBackupPathAsync(host, folder);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var backups = scope.ServiceProvider.GetRequiredService<IScheduledBackupService>();
            for (var i = 0; i < 3; i++)
            {
                Assert.NotNull(await backups.RunAsync(Ct));
            }
        }

        var super = await SuperClientAsync(host);
        var history = await super.GetJsonAsync<PagedResult<ScheduledDumpDto>>("api/scheduled-dumps?includeTotal=true");
        Assert.Equal(2, history.TotalCount);
        Assert.Equal(2, history.Items.Count);
    }

    [Fact]
    public async Task A_dump_failure_in_the_worker_is_recorded_as_a_system_error()
    {
        var marker = HttpClientExtensions.Unique("dumpfail");
        var folder = NewTempFolder();
        using var host = CreateHost(keepCount: 3, dump: new FailingDumpRunner(marker), startWorkerAutomatically: false);
        await SetBackupPathAsync(host, folder);

        var worker = host.Services.GetRequiredService<ScheduledBackupWorker>();
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        var super = await SuperClientAsync(host);
        SystemErrorListItemDto? item = null;
        for (var i = 0; i < 40 && item is null; i++)
        {
            await Task.Delay(50, Ct);
            var page = await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={marker}");
            item = page.Items.FirstOrDefault(e => e.Message.Contains(marker, StringComparison.Ordinal));
        }

        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(item);
        var details = await super.GetJsonAsync<SystemErrorDto>($"api/system-errors/{item.Id}");
        Assert.Contains(nameof(ScheduledBackupWorker), details.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ScheduledBackup:KeepCount", "0", nameof(ScheduledBackupOptions.KeepCount))]
    [InlineData("ScheduledBackup:IntervalDays", "0", nameof(ScheduledBackupOptions.IntervalDays))]
    public void Api_does_not_start_with_an_invalid_scheduled_backup_option(string key, string value, string optionName)
    {
        using var badFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));

        var error = Record.Exception(() => badFactory.CreateClient());

        Assert.NotNull(error);
        Assert.True(error.ToString().Contains(optionName), error.ToString());
    }

    private WebApplicationFactory<Program> CreateHost(
        int keepCount,
        IDatabaseDumpRunner? dump = null,
        bool startWorkerAutomatically = true) =>
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
                services.AddSingleton<IDatabaseDumpRunner>(dump ?? new FakeDumpRunner());
                if (!startWorkerAutomatically)
                {
                    // Keep the worker constructible, but do not let the host start it before the backup path is set.
                    foreach (var descriptor in services.Where(d =>
                                 d.ServiceType == typeof(IHostedService)
                                 && d.ImplementationType == typeof(ScheduledBackupWorker)).ToList())
                    {
                        services.Remove(descriptor);
                    }

                    services.AddSingleton<ScheduledBackupWorker>();
                }
            });
        });

    private async Task<HttpClient> SuperClientAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        // Startup may fill an empty backup path in the shared DB; drop the parent factory's cached Version.
        factory.Services.RefreshSystemSettingsCache();
        client.WithBearer(await factory.GetAccessTokenAsync(TestUsers.Super));
        return client;
    }

    private async Task SetBackupPathAsync(WebApplicationFactory<Program> host, string folder)
    {
        var super = await SuperClientAsync(host);
        host.Services.RefreshSystemSettingsCache();
        factory.Services.RefreshSystemSettingsCache();
        var current = await super.GetJsonAsync<SystemSettingsDto>("api/system-settings");
        var response = await super.PutAsJsonAsync(
            "api/system-settings",
            new UpdateSystemSettingsRequest { BackupPath = folder, Version = current.Version },
            Ct);
        response.EnsureSuccessStatusCode();
        // Derived hosts have their own cache; the shared ApiFactory cache would keep a stale Version.
        factory.Services.RefreshSystemSettingsCache();
    }

    private async Task ClearBackupPathAsync(WebApplicationFactory<Program> host)
    {
        var super = await SuperClientAsync(host);
        host.Services.RefreshSystemSettingsCache();
        factory.Services.RefreshSystemSettingsCache();
        var current = await super.GetJsonAsync<SystemSettingsDto>("api/system-settings");
        var response = await super.PutAsJsonAsync(
            "api/system-settings",
            new UpdateSystemSettingsRequest { BackupPath = " ", Version = current.Version },
            Ct);
        response.EnsureSuccessStatusCode();
        factory.Services.RefreshSystemSettingsCache();
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

    private sealed class FailingDumpRunner(string marker) : IDatabaseDumpRunner
    {
        public Task RunAsync(string outputFilePath, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"scheduled-backup-failed-{marker}");
    }
}
