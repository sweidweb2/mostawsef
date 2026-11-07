using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Patients;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Patients;

[Collection(ApiCollection.Name)]
public class PatientsTests(ApiFactory factory)
{
    private const string Url = "api/patients";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_creates_a_patient_and_reads_it_back()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"Mona Adel {Guid.NewGuid():N}";

        var response = await admin.PostAsJsonAsync(Url, new CreatePatientRequest
        {
            FullName = $"  {name} ",
            Phone = " ٠١٠  ١٢٣٤ ٥٦٧٨ ",
            GuardianPhone = "  ",
            Notes = "  Allergic to penicillin ",
            IsInsured = true,
            Gender = Genders.Female,
            DateOfBirth = new DateOnly(2015, 3, 9),
        }, Ct);
        var created = await response.Content.ReadFromJsonAsync<PatientDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Url}/{created!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(name, created.FullName);
        Assert.Equal("010 1234 5678", created.Phone);
        Assert.Null(created.GuardianPhone);
        Assert.Equal("Allergic to penicillin", created.Notes);
        Assert.True(created.IsInsured);
        Assert.Equal((Genders.Female, new DateOnly(2015, 3, 9)), (created.Gender, created.DateOfBirth));
        Assert.Equal(0, created.Version);
        var reread = await admin.GetJsonAsync<PatientDto>($"{Url}/{created.Id}");
        Assert.Equal(created, reread with { CreatedAt = created.CreatedAt });
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_an_error_code_per_field()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreatePatientRequest
        {
            FullName = " ",
            Phone = "call me",
            GuardianPhone = new string('1', 31),
            Notes = new string('n', 2001),
            Gender = "Other",
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Required], problem.Errors["FullName"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Phone"]);
        Assert.Equal([$"max_length|{PatientRules.PhoneMaxLength}"], problem.Errors["GuardianPhone"]);
        Assert.Equal([$"max_length|{PatientRules.NotesMaxLength}"], problem.Errors["Notes"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Gender"]);
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    [InlineData(2999, 1, 1)]
    public async Task A_date_of_birth_before_1900_or_in_the_future_returns_400(int year, int month, int day)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, Valid() with { DateOfBirth = new DateOnly(year, month, day) }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["DateOfBirth"]);
    }

    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync(Url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", Url)]
    [InlineData("GET", Url + "/{id}")]
    [InlineData("POST", Url)]
    [InlineData("PUT", Url + "/{id}")]
    [InlineData("DELETE", Url + "/{id}")]
    public async Task Super_gets_403_on_every_patient_endpoint(string method, string url)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
        };

        var response = await super.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_patient_returns_404_for_read_update_and_delete()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var url = $"{Url}/{Guid.NewGuid()}";

        var responses = new[]
        {
            await admin.GetAsync(url, Ct),
            await admin.PutAsJsonAsync(url, Update(Valid(), 0), Ct),
            await admin.DeleteAsync(url, Ct),
        };

        foreach (var response in responses)
        {
            Assert.Equal(ErrorCodes.NotFound, (await ProblemResponse.ReadAsync(response)).Code);
        }
    }

    [Fact]
    public async Task Update_changes_every_field_and_records_who_did_it()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);

        var response = await admin.PutAsJsonAsync($"{Url}/{patient.Id}", new UpdatePatientRequest
        {
            FullName = "Renamed", Phone = "0111", GuardianPhone = "0122", Notes = "New note", IsInsured = true,
            Gender = Genders.Female, DateOfBirth = new DateOnly(1990, 1, 1), Version = patient.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<PatientDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ("Renamed", "0111", "0122", "New note", true, Genders.Female, (DateOnly?)new DateOnly(1990, 1, 1)),
            (updated!.FullName, updated.Phone, updated.GuardianPhone, updated.Notes, updated.IsInsured, updated.Gender, updated.DateOfBirth));
        Assert.Equal(patient.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Admin.Username, updated.UpdatedBy);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_returns_409_concurrency_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);
        (await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid(), patient.Version), Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid(), patient.Version), Ct));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
    }

    [Fact]
    public async Task Delete_hides_the_patient_but_keeps_the_row()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);

        var deleted = await admin.DeleteAsync($"{Url}/{patient.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{patient.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(
            (await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(patient.FullName)}")).Items,
            p => p.Id == patient.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Patients
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == patient.Id, Ct);
        Assert.True(row.IsDeleted);
        Assert.Equal(TestUsers.Admin.Username, row.DeletedBy);
    }

    [Theory]
    [InlineData("فاطمة")]
    [InlineData("فاطِمه")]
    public async Task Search_finds_arabic_names_however_they_were_typed(string term)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin, $"فاطمة {Guid.NewGuid():N}");

        var page = await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.Contains(page.Items, p => p.Id == patient.Id);
    }

    [Fact]
    public async Task Search_matches_the_phone_and_the_guardian_phone_in_any_digits()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var digits = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var byPhone = await CreateAsync(admin, phone: "01" + digits);
        var byGuardian = await CreateAsync(admin, guardianPhone: "02" + digits);
        var arabicDigits = string.Concat(digits.Select(d => (char)('\u0660' + (d - '0'))));

        var page = await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(arabicDigits)}");

        Assert.Equal(new[] { byGuardian.Id, byPhone.Id }.Order(), page.Items.Select(p => p.Id).Order());
    }

    [Fact]
    public async Task Patient_actions_are_logged_without_medical_notes()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);
        await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid() with { Notes = "Diabetic" }, patient.Version), Ct);
        await admin.DeleteAsync($"{Url}/{patient.Id}", Ct);

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=patients.&pageSize=100");
        var actions = new List<string>();
        foreach (var item in page.Items)
        {
            var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{item.Id}");
            if (details.EntityId == patient.Id.ToString())
            {
                actions.Add(details.Action);
                Assert.DoesNotContain("Diabetic", details.Details ?? string.Empty);
            }
        }

        Assert.Equal(["patients.created", "patients.deleted", "patients.updated"], actions.Order().ToArray());
    }

    private static CreatePatientRequest Valid() => new()
    {
        FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male,
    };

    private static UpdatePatientRequest Update(CreatePatientRequest request, int version) => new()
    {
        FullName = request.FullName, Phone = request.Phone, GuardianPhone = request.GuardianPhone, Notes = request.Notes,
        IsInsured = request.IsInsured, Gender = request.Gender, DateOfBirth = request.DateOfBirth, Version = version,
    };

    private static async Task<PatientDto> CreateAsync(HttpClient admin, string? fullName = null, string? phone = null, string? guardianPhone = null)
    {
        var response = await admin.PostAsJsonAsync(Url, Valid() with
        {
            FullName = fullName ?? $"Patient {Guid.NewGuid():N}", Phone = phone ?? "01000000000", GuardianPhone = guardianPhone,
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
    }
}
