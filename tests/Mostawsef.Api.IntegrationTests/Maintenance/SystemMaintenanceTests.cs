using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
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
    public async Task The_dashboard_of_today_counts_its_order_patient_and_payment_and_lists_every_status()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var url = $"api/system/dashboard?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}";
        var before = await super.GetJsonAsync<SuperDashboardDto>(url);

        var test = await CreateTestAsync(admin);
        await CreateOrderAsync(admin, test.Id, total: 5000, paid: 2000);

        var after = await super.GetJsonAsync<SuperDashboardDto>(url);

        Assert.Equal((today, today, DashboardBuckets.Hour), (after.Range.From, after.Range.To, after.Range.Bucket));
        Assert.Equal(24, after.Points.Count);
        Assert.Equal(before.Orders + 1, after.Orders);
        Assert.Equal(before.Patients + 1, after.Patients);
        Assert.Equal(before.LabTests + 1, after.LabTests);
        Assert.Equal(before.Revenue + 2000, after.Revenue);
        Assert.Equal(before.Outstanding + 3000, after.Outstanding);
        Assert.True(after.Activity > before.Activity);
        Assert.Equal(after.Orders, after.Points.Sum(p => p.Orders));
        Assert.Equal(after.Revenue, after.Points.Sum(p => p.Revenue));
        Assert.Equal(OrderStatuses.All, after.OrderStatuses.Select(s => s.Status));
        Assert.Equal(
            before.OrderStatuses.Single(s => s.Status == OrderStatuses.PartiallyPaid).Count + 1,
            after.OrderStatuses.Single(s => s.Status == OrderStatuses.PartiallyPaid).Count);
        Assert.True(after.Users >= TestUsers.SeededLogins.Length + 1); // Super seed(s) + clinic identity
    }

    [Fact]
    public async Task The_dashboard_period_stays_between_the_first_record_and_today_and_rejects_from_after_to()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var wide = await super.GetJsonAsync<SuperDashboardDto>("api/system/dashboard?from=2000-01-01&to=2999-12-31");
        Assert.Equal((wide.Range.FirstDate, wide.Range.Today), (wide.Range.From, wide.Range.To));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), wide.Range.Today);

        var problem = await ProblemResponse.ReadAsync(
            await super.GetAsync("api/system/dashboard?from=2026-02-02&to=2026-02-01", Ct));
        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["From"]);
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
        var test = await CreateTestAsync(admin);
        var order = await CreateOrderAsync(admin, test.Id, total: 1000, paid: 1000);
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
        var next = await CreateOrderAsync(admin, (await CreateTestAsync(admin)).Id, total: 500, paid: 0);
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

    private static async Task<LabTestDto> CreateTestAsync(HttpClient admin)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync(
            "api/lab-tests", new CreateLabTestRequest { Code = code, Name = $"Test {code}", LabCost = 0 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }

    /// <summary>A new patient's order of one test charged <paramref name="total"/>, with a first payment when <paramref name="paid"/> is above 0.</summary>
    private static async Task<OrderDetailsDto> CreateOrderAsync(HttpClient admin, Guid testId, long total, long paid)
    {
        var response = await admin.PostAsJsonAsync(
            "api/orders",
            new CreateOrderRequest
            {
                NewPatient = new CreatePatientRequest { FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male },
                Items = [new() { LabTestId = testId }],
                Addon = total,
                FirstPayment = paid > 0 ? new() { Amount = paid, Method = PaymentMethods.Cash } : null,
            },
            Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct))!;
    }
}
