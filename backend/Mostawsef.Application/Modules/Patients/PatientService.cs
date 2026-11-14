using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Patients;

/// <summary>Patient records (Admin portal). Failures throw <see cref="AppException"/>.</summary>
public interface IPatientService
{
    Task<PagedResult<PatientDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    /// <summary>Up to <see cref="PageQuery.MaxExportRows"/> patients for print/export (no notes).</summary>
    Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken);

    Task<PatientDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PatientDto> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken);

    Task<PatientDto> UpdateAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class PatientService(IPatientRepository patients, IActivityLogger activity, TimeProvider time) : IPatientService
{
    public async Task<PagedResult<PatientDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        query.EnsureSearchable();
        var (items, total) = await patients.GetPageAsync(query, cancellationToken);
        return new PagedResult<PatientDto>(items.Select(ToListDto).ToList(), query.SafePage, query.SafePageSize, total);
    }

    public async Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken) =>
        (await patients.ListAllAsync(cancellationToken)).Select(ToExportDto).ToList();

    public async Task<PatientDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await patients.GetDetailsAsync(id, cancellationToken) ?? throw AppException.NotFound();
        return new PatientDto(
            patient.Id, patient.FullName, patient.Phone, patient.GuardianPhone, patient.Notes, patient.IsInsured,
            patient.Gender.ToString(), patient.DateOfBirth, patient.CreatedAt, patient.UpdatedAt, patient.UpdatedBy, patient.Version);
    }

    public async Task<PatientDto> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        PatientInput.EnsureValidDateOfBirth(request.DateOfBirth, time, nameof(CreatePatientRequest.DateOfBirth));

        var patient = PatientInput.Create(request);
        patients.Add(patient);
        await patients.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.PatientCreated, "Created a patient", patient, cancellationToken);
        return ToDto(patient);
    }

    public async Task<PatientDto> UpdateAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = await FindAsync(id, cancellationToken);
        if (patient.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        PatientInput.EnsureValidDateOfBirth(request.DateOfBirth, time, nameof(CreatePatientRequest.DateOfBirth));

        PatientInput.Apply(patient, request);
        await patients.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.PatientUpdated, "Updated a patient", patient, cancellationToken);
        return ToDto(patient);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await FindAsync(id, cancellationToken);
        patients.Remove(patient);
        await patients.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.PatientDeleted, "Deleted a patient", patient, cancellationToken);
    }

    private async Task<Patient> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await patients.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();

    /// <summary>Only the name: notes and other details are medical data and stay out of the activity log.</summary>
    private Task LogAsync(string action, string description, Patient patient, CancellationToken cancellationToken) =>
        activity.LogAsync(
            new ActivityEntry(action, ActivityModules.Patients, description)
            {
                EntityType = nameof(Patient),
                EntityId = patient.Id.ToString(),
                Details = new { patient.FullName },
            },
            cancellationToken);

    private static PatientDto ToListDto(PatientListRow p) => new(
        p.Id, p.FullName, p.Phone, p.GuardianPhone, Notes: null, p.IsInsured, p.Gender.ToString(), p.DateOfBirth,
        p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version, p.HasUnpaidOrders);

    private static PatientDto ToExportDto(PatientExportRow p) => new(
        p.Id, p.FullName, p.Phone, p.GuardianPhone, Notes: null, p.IsInsured, p.Gender.ToString(), p.DateOfBirth,
        default, null, null, 0);

    private static PatientDto ToDto(Patient p) => new(
        p.Id, p.FullName, p.Phone, p.GuardianPhone, p.Notes, p.IsInsured, p.Gender.ToString(), p.DateOfBirth,
        p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version);
}
