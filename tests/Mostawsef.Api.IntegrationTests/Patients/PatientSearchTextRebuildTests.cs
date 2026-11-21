using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Patients;

[Collection(ApiCollection.Name)]
public class PatientSearchTextRebuildTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stale_search_text_with_old_guardian_digits_is_rebuilt()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var response = await admin.PostAsJsonAsync("api/patients", new CreatePatientRequest
        {
            FullName = $"Patient {Guid.NewGuid():N}",
            Phone = "01011112222",
            Gender = Genders.Female,
        }, Ct);
        response.EnsureSuccessStatusCode();
        var patient = (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var searchName = await db.Patients.IgnoreQueryFilters()
            .Where(p => p.Id == patient.Id)
            .Select(p => p.SearchName)
            .SingleAsync(Ct);
        // Simulate a pre-drop SearchText that still contained a second phone.
        await db.Patients.IgnoreQueryFilters().Where(p => p.Id == patient.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.SearchText, searchName + " 01011112222 01099998888"), Ct);

        var updated = await PatientSearchTextRebuild.RunAsync(db, Ct);
        var again = await PatientSearchTextRebuild.RunAsync(db, Ct);

        Assert.True(updated >= 1);
        Assert.Equal(0, again);
        var saved = await db.Patients.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.Id == patient.Id)
            .Select(p => p.SearchText)
            .SingleAsync(Ct);
        Assert.Equal(Patient.ComposeSearchText(searchName, "01011112222"), saved);
        Assert.True(await db.Set<NumberSequence>()
            .AnyAsync(s => s.Name == PatientSearchTextRebuild.CompletionMarker, Ct));
    }
}
