using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Contracts.Settings;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Api.IntegrationTests.Maintenance;

[Collection(ApiCollection.Name)]
public class SystemMaintenanceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dashboard_counts_todays_order_patient_and_payment_and_lists_every_status()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var before = await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard?days=30");

        var test = await CreateTestAsync(admin, price: 5000);
        await CreateOrderAsync(admin, test.Id, paid: 2000);

        var after = await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard?days=30");

        Assert.Equal(30, after.Days.Count);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), after.Days[^1].Date);
        Assert.Equal(before.Orders + 1, after.Orders);
        Assert.Equal(before.Patients + 1, after.Patients);
        Assert.Equal(before.LabTests + 1, after.LabTests);
        Assert.Equal(before.Revenue + 2000, after.Revenue);
        Assert.Equal(before.Outstanding + 3000, after.Outstanding);
        Assert.Equal(before.Days[^1].Orders + 1, after.Days[^1].Orders);
        Assert.Equal(before.Days[^1].Patients + 1, after.Days[^1].Patients);
        Assert.Equal(before.Days[^1].Revenue + 2000, after.Days[^1].Revenue);
        Assert.True(after.Days[^1].Activity > before.Days[^1].Activity);
        Assert.Equal(OrderStatuses.All, after.OrderStatuses.Select(s => s.Status));
        Assert.Equal(
            before.OrderStatuses.Single(s => s.Status == OrderStatuses.PartiallyPaid).Count + 1,
            after.OrderStatuses.Single(s => s.Status == OrderStatuses.PartiallyPaid).Count);
        Assert.True(after.Users >= TestUsers.All.Length);
    }

    [Fact]
    public async Task Dashboard_days_are_kept_between_7_and_90()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        Assert.Equal(7, (await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard?days=1")).Days.Count);
        Assert.Equal(90, (await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard?days=500")).Days.Count);
    }

    [Fact]
    public async Task Only_super_users_reach_the_dashboard_and_the_reset()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("api/system/dashboard", Ct)).StatusCode);
        var reset = await admin.PostAsJsonAsync(
            "api/system/reset-data", new ResetDataRequest { Confirmation = DataResetRules.ConfirmationWord }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("api/system/dashboard", Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("reset")]
    [InlineData("DELETE")]
    public async Task Reset_without_the_exact_confirmation_word_is_refused_and_deletes_nothing(string? word)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);

        var response = await super.PostAsJsonAsync("api/system/reset-data", new ResetDataRequest { Confirmation = word }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await admin.GetAsync($"api/patients/{patient.Id}", Ct)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Reset_deletes_all_data_but_keeps_accounts_and_settings()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var (createdUser, createdLogin) = await super.CreateUserAsync();
        var test = await CreateTestAsync(admin, price: 1000);
        var order = await CreateOrderAsync(admin, test.Id, paid: 1000);
        var clinicBefore = await admin.GetJsonAsync<ClinicSettingsDto>("api/clinic-settings");

        var response = await super.PostAsJsonAsync(
            "api/system/reset-data", new ResetDataRequest { Confirmation = DataResetRules.ConfirmationWord }, Ct);
        response.EnsureSuccessStatusCode();
        var removed = (await response.Content.ReadFromJsonAsync<ResetDataResultDto>(Ct))!;

        Assert.True(removed.Orders >= 1);
        Assert.True(removed.Patients >= 1);
        Assert.True(removed.Payments >= 1);
        Assert.True(removed.LabTests >= 1);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"api/orders/{order.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"api/lab-tests/{test.Id}", Ct)).StatusCode);

        var dashboard = await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard");
        Assert.Equal((0, 0, 0, 0L, 0L), (dashboard.Orders, dashboard.Patients, dashboard.LabTests, dashboard.Revenue, dashboard.Outstanding));

        // Only the reset itself is left in the activity log.
        var activity = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs");
        var entry = Assert.Single(activity.Items);
        Assert.Equal("system.data_reset", entry.Action);
        Assert.Equal(TestUsers.Super.Username, entry.Username);
        Assert.Equal(0, (await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats")).Total);

        // Accounts and settings stay: existing and new sign-ins work, and the clinic settings are unchanged.
        Assert.NotNull(await super.GetJsonAsync<UserDetailsDto>($"api/users/{createdUser.Id}"));
        Assert.True((await factory.CreateClient().LoginAsync(createdLogin.Username, createdLogin.Password)).IsSuccessStatusCode);
        Assert.Equal(clinicBefore.Name, (await admin.GetJsonAsync<ClinicSettingsDto>("api/clinic-settings")).Name);

        // Numbering starts again and new data can be added right away.
        var next = await CreateOrderAsync(admin, (await CreateTestAsync(admin, price: 500)).Id, paid: 0);
        Assert.Equal("ORD-000001", next.Number);
    }

    private static async Task<PatientDto> CreatePatientAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync(
            "api/patients",
            new CreatePatientRequest { FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male },
            Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
    }

    private static async Task<LabTestDto> CreateTestAsync(HttpClient admin, long price)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync(
            "api/lab-tests", new CreateLabTestRequest { Code = code, Name = $"Test {code}", Price = price, LabCost = 0 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }

    /// <summary>A new patient's order of one test, with a first payment when <paramref name="paid"/> is above 0.</summary>
    private static async Task<OrderDetailsDto> CreateOrderAsync(HttpClient admin, Guid testId, long paid)
    {
        var response = await admin.PostAsJsonAsync(
            "api/orders",
            new CreateOrderRequest
            {
                NewPatient = new CreatePatientRequest { FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male },
                Items = [new() { LabTestId = testId }],
                FirstPayment = paid > 0 ? new() { Amount = paid, Method = PaymentMethods.Cash } : null,
            },
            Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct))!;
    }
}
