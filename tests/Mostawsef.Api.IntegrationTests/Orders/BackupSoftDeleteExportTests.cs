using System.Net.Http.Json;
using System.Text;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Patients;

namespace Mostawsef.Api.IntegrationTests.Orders;

[Collection(ApiCollection.Name)]
public class BackupSoftDeleteExportTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Soft_deleted_patients_are_not_exported()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var marker = $"SoftDel {Guid.NewGuid():N}";
        var create = await admin.PostAsJsonAsync(
            "api/patients",
            new CreatePatientRequest { FullName = marker, Phone = "01000000001", Gender = Genders.Male },
            Ct);
        create.EnsureSuccessStatusCode();
        var patient = (await create.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
        (await admin.DeleteAsync($"api/patients/{patient.Id}", Ct)).EnsureSuccessStatusCode();

        var saved = await super.PostAsJsonAsync(
            "api/backups",
            new CreateBackupRequest { Format = BackupFormats.Csv, Table = "patients" },
            Ct);
        saved.EnsureSuccessStatusCode();
        var record = (await saved.Content.ReadFromJsonAsync<BackupRecordDto>(Ct))!;

        var export = await super.GetAsync($"api/backups/{record.Id}/export", Ct);
        export.EnsureSuccessStatusCode();
        var csv = Encoding.UTF8.GetString(await export.Content.ReadAsByteArrayAsync(Ct));

        Assert.DoesNotContain(marker, csv, StringComparison.Ordinal);
    }
}
