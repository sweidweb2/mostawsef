using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Api.IntegrationTests.Settings;

/// <summary>The settings row is shared by the whole test run: each test reads the current Version before changing it.</summary>
[Collection(ApiCollection.Name)]
public class ClinicSettingsTests(ApiFactory factory)
{
    private const string Url = "api/clinic-settings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Admin")]
    [InlineData("Super")]
    public async Task Every_signed_in_role_can_read_the_settings(string role)
    {
        var client = await factory.CreateClientAsAsync(role == "Admin" ? TestUsers.Admin : TestUsers.Super);

        var settings = await client.GetJsonAsync<ClinicSettingsDto>(Url);

        Assert.False(string.IsNullOrWhiteSpace(settings.Name));
        Assert.Contains(settings.CurrencyCode, Currencies.All);
    }

    [Fact]
    public async Task Signed_out_users_get_401()
    {
        var response = await factory.CreateClient().GetAsync(Url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_second_read_reuses_the_cached_settings_row()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        await admin.GetJsonAsync<ClinicSettingsDto>(Url);

        factory.Sql.Clear();
        await admin.GetJsonAsync<ClinicSettingsDto>(Url);

        Assert.DoesNotContain(
            factory.Sql.Queries,
            q => q.Contains("clinic_settings", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Admin_updates_the_settings_and_the_change_is_audited()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var current = await admin.GetJsonAsync<ClinicSettingsDto>(Url);
        var name = $"Clinic {Guid.NewGuid():N}"[..20];

        var response = await admin.PutAsJsonAsync(Url, new UpdateClinicSettingsRequest
        {
            Name = $"  {name} ",
            Address = "12 Nile St, Cairo",
            Phone = " ",
            CurrencyCode = Currencies.Usd,
            Version = current.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<ClinicSettingsDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(name, updated!.Name);
        Assert.Equal("12 Nile St, Cairo", updated.Address);
        Assert.Null(updated.Phone);
        Assert.Equal(Currencies.Usd, updated.CurrencyCode);
        Assert.Equal(current.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Admin.Username, updated.UpdatedBy);
        var reread = await admin.GetJsonAsync<ClinicSettingsDto>(Url);
        Assert.Equal((updated.Name, updated.Version), (reread.Name, reread.Version));

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var log = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=settings.clinic_updated");
        Assert.Contains(log.Items, i => i.Username == TestUsers.Admin.Username && i.Module == "Settings");
    }

    [Fact]
    public async Task Super_cannot_change_the_settings()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var current = await super.GetJsonAsync<ClinicSettingsDto>(Url);

        var response = await super.PutAsJsonAsync(Url, new UpdateClinicSettingsRequest { Name = "X", Version = current.Version }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_codes()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(
            Url, new UpdateClinicSettingsRequest { Name = " ", Phone = new string('1', 31), CurrencyCode = "XYZ" }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Required], problem.Errors["Name"]);
        Assert.Equal([$"max_length|{ClinicSettingsRules.PhoneMaxLength}"], problem.Errors["Phone"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["CurrencyCode"]);
    }

    [Fact]
    public async Task A_stale_version_returns_409()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var current = await admin.GetJsonAsync<ClinicSettingsDto>(Url);
        var request = new UpdateClinicSettingsRequest { Name = current.Name, CurrencyCode = current.CurrencyCode, Version = current.Version };
        (await admin.PutAsJsonAsync(Url, request with { Name = $"Edited {Guid.NewGuid():N}"[..20] }, Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync(Url, request, Ct));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
    }
}
