using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Settings;
using Mostawsef.Infrastructure.Persistence;

namespace Mostawsef.Api.IntegrationTests.Settings;

/// <summary>
/// The settings row is shared by the whole test run: each test reads the current Version before changing it,
/// and maintenance mode is always switched off again.
/// </summary>
[Collection(ApiCollection.Name)]
public class SystemSettingsTests(ApiFactory factory)
{
    private const string Url = "api/system-settings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_second_read_reuses_the_cached_settings_row()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        await super.GetJsonAsync<SystemSettingsDto>(Url);

        factory.Sql.Clear();
        await super.GetJsonAsync<SystemSettingsDto>(Url);

        Assert.DoesNotContain(
            factory.Sql.Queries,
            q => q.Contains("system_settings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Super_updates_the_settings_and_the_change_is_audited()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var current = await super.GetJsonAsync<SystemSettingsDto>(Url);
        var path = $@"D:\Backups\{Guid.NewGuid():N}";

        var response = await super.PutAsJsonAsync(Url, new UpdateSystemSettingsRequest
        {
            BackupPath = $"  {path} ",
            MaintenanceMode = false,
            Version = current.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<SystemSettingsDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, updated!.BackupPath);
        Assert.Equal(current.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Super.Username, updated.UpdatedBy);
        var reread = await super.GetJsonAsync<SystemSettingsDto>(Url);
        Assert.Equal((updated.BackupPath, updated.Version), (reread.BackupPath, reread.Version));

        var log = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=settings.system_updated");
        Assert.Contains(log.Items, i => i.Username == TestUsers.Super.Username && i.Module == "Settings");
    }

    [Fact]
    public async Task A_blank_backup_path_is_stored_as_empty()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var current = await super.GetJsonAsync<SystemSettingsDto>(Url);

        var updated = await (await super.PutAsJsonAsync(
                Url, new UpdateSystemSettingsRequest { BackupPath = "   ", Version = current.Version }, Ct))
            .Content.ReadFromJsonAsync<SystemSettingsDto>(Ct);

        Assert.Null(updated!.BackupPath);
    }

    [Fact]
    public async Task An_empty_backup_path_is_filled_with_the_api_Backups_folder_on_initialize()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var current = await super.GetJsonAsync<SystemSettingsDto>(Url);
        var cleared = await super.PutAsJsonAsync(
            Url, new UpdateSystemSettingsRequest { BackupPath = " ", Version = current.Version }, Ct);
        cleared.EnsureSuccessStatusCode();

        await factory.Services.InitializeDatabaseAsync([], Ct);
        factory.Services.GetRequiredService<ISystemSettingsCache>().Invalidate();

        var restored = await super.GetJsonAsync<SystemSettingsDto>(Url);
        Assert.Equal(ScheduledBackupPaths.DefaultFolder, restored.BackupPath);
    }

    [Fact]
    public async Task Admin_cannot_read_or_change_the_settings()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var read = await admin.GetAsync(Url, Ct);
        var write = await admin.PutAsJsonAsync(Url, new UpdateSystemSettingsRequest(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Signed_out_users_get_401()
    {
        var response = await factory.CreateClient().GetAsync(Url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_too_long_backup_path_returns_400_with_the_limit()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var problem = await ProblemResponse.ReadAsync(await super.PutAsJsonAsync(
            Url, new UpdateSystemSettingsRequest { BackupPath = new string('a', SystemSettingsRules.BackupPathMaxLength + 1) }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([$"max_length|{SystemSettingsRules.BackupPathMaxLength}"], problem.Errors["BackupPath"]);
    }

    [Fact]
    public async Task A_stale_version_returns_409()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var current = await super.GetJsonAsync<SystemSettingsDto>(Url);
        var request = new UpdateSystemSettingsRequest { BackupPath = current.BackupPath, Version = current.Version };
        (await super.PutAsJsonAsync(Url, request with { BackupPath = @"E:\Edited" }, Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await super.PutAsJsonAsync(Url, request, Ct));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
    }

    [Fact]
    public async Task Maintenance_mode_blocks_new_admin_sign_ins_but_not_super_or_existing_sessions()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var signedInAdmin = await factory.CreateClientAsAsync(TestUsers.Admin);

        await SetMaintenanceModeAsync(super, true);
        try
        {
            var blocked = await ProblemResponse.ReadAsync(
                await factory.CreateClient().LoginAsync(TestUsers.Admin.Username, TestUsers.Admin.Password));
            var wrongPassword = await factory.CreateClient().LoginAsync(TestUsers.Admin.Username, "wrong-password");
            var superLogin = await factory.CreateClient().LoginAsync(TestUsers.Super.Username, TestUsers.Super.Password);
            var existingSession = await signedInAdmin.GetAsync("api/clinic-settings", Ct);

            Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
            Assert.Equal(ErrorCodes.MaintenanceMode, blocked.Code);
            Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
            Assert.Equal(HttpStatusCode.OK, superLogin.StatusCode);
            Assert.Equal(HttpStatusCode.OK, existingSession.StatusCode);

            var log = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=auth.sign_in_blocked");
            Assert.Contains(log.Items, i => i.Username == TestUsers.Admin.Username);
        }
        finally
        {
            await SetMaintenanceModeAsync(super, false);
        }

        var afterwards = await factory.CreateClient().LoginAsync(TestUsers.Admin.Username, TestUsers.Admin.Password);
        Assert.Equal(HttpStatusCode.OK, afterwards.StatusCode);
    }

    private static async Task SetMaintenanceModeAsync(HttpClient super, bool on)
    {
        var current = await super.GetJsonAsync<SystemSettingsDto>(Url);
        var response = await super.PutAsJsonAsync(
            Url, new UpdateSystemSettingsRequest { BackupPath = current.BackupPath, MaintenanceMode = on, Version = current.Version }, Ct);
        response.EnsureSuccessStatusCode();
    }
}
