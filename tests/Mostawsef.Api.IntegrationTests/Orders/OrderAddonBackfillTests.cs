using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Orders;

[Collection(ApiCollection.Name)]
public class OrderAddonBackfillTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Legacy_totals_that_are_not_the_lab_sum_are_copied_into_addon()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin, labCost: 2000);
        var order = await CreateOrderAsync(admin, patient.Id, test.Id, addon: 5000);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Unfinished legacy shape: patient charge was our price alone, Addon column still 0.
        await db.Orders.Where(o => o.Id == order.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Addon, 0L).SetProperty(o => o.Total, 9000L), Ct);

        var updated = await OrderAddonBackfill.RunAsync(db, Ct);

        Assert.True(updated >= 1);
        var saved = await db.Orders.AsNoTracking().Where(o => o.Id == order.Id)
            .Select(o => new { o.Total, o.Addon }).SingleAsync(Ct);
        Assert.Equal((9000L, 9000L), (saved.Total, saved.Addon));
        Assert.True(await db.Set<NumberSequence>().AnyAsync(s => s.Name == OrderAddonBackfill.CompletionMarker, Ct));
    }

    [Fact]
    public async Task New_zero_addon_orders_are_not_rewritten_when_the_backfill_runs_again()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin, labCost: 3500);
        var order = await CreateOrderAsync(admin, patient.Id, test.Id, addon: null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Orders.AsNoTracking().Where(o => o.Id == order.Id)
            .Select(o => new { o.Total, o.Addon }).SingleAsync(Ct);
        Assert.Equal((3500L, 0L), (before.Total, before.Addon));

        await OrderAddonBackfill.RunAsync(db, Ct);
        await OrderAddonBackfill.RunAsync(db, Ct); // marker already present / second host

        var saved = await db.Orders.AsNoTracking().Where(o => o.Id == order.Id)
            .Select(o => new { o.Total, o.Addon }).SingleAsync(Ct);
        Assert.Equal((3500L, 0L), (saved.Total, saved.Addon));
    }

    private static async Task<OrderDetailsDto> CreateOrderAsync(HttpClient admin, Guid patientId, Guid testId, long? addon)
    {
        var response = await admin.PostAsJsonAsync("api/orders", new CreateOrderRequest
        {
            PatientId = patientId,
            Items = [new() { LabTestId = testId }],
            Addon = addon,
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct))!;
    }

    private static async Task<PatientDto> CreatePatientAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("api/patients", new CreatePatientRequest
        {
            FullName = $"Patient {Guid.NewGuid():N}",
            Phone = "01000000000",
            Gender = Genders.Male,
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
    }

    private static async Task<LabTestDto> CreateTestAsync(HttpClient admin, long labCost)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync(
            "api/lab-tests", new CreateLabTestRequest { Code = code, Name = $"Test {code}", LabCost = labCost }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }
}
