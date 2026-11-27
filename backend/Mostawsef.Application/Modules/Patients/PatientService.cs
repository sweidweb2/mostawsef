using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Patients;

/// <summary>Patient records (Admin portal). Failures throw <see cref="AppException"/>.</summary>
public interface IPatientService
{
    /// <param name="includeUnpaid">Fills <see cref="PatientDto.HasUnpaidOrders"/> on each row (the Patients page); null otherwise.</param>
    Task<PagedResult<PatientDto>> GetPageAsync(PageQuery query, bool includeUnpaid, CancellationToken cancellationToken);

    /// <summary>Up to <see cref="PageQuery.MaxExportRows"/> patients for print/export (no notes).</summary>
    Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken);

    Task<PatientDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Full patient plus newest orders (one round-trip for the details page).</summary>
    Task<PatientDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<PatientDto> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken);

    Task<PatientDto> UpdateAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class PatientService(IPatientRepository patients, IActivityLogger activity, TimeProvider time) : IPatientService
{
    public async Task<PagedResult<PatientDto>> GetPageAsync(PageQuery query, bool includeUnpaid, CancellationToken cancellationToken)
    {
        query.EnsureSearchable();
        var (items, total) = await patients.GetPageAsync(query, includeUnpaid, cancellationToken);
        return new PagedResult<PatientDto>(items.Select(ToListDto).ToList(), query.SafePage, query.SafePageSize, total);
    }

    public async Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken) =>
        (await patients.ListAllAsync(cancellationToken)).Select(ToExportDto).ToList();

    public async Task<PatientDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await patients.GetDetailsAsync(id, cancellationToken) ?? throw AppException.NotFound();
        return new PatientDto(
            patient.Id, patient.FullName, patient.Phone, patient.Notes,
            patient.Gender.ToString(), patient.DateOfBirth, patient.CreatedAt, patient.UpdatedAt, patient.UpdatedBy, patient.Version);
    }

    public async Task<PatientDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await GetAsync(id, cancellationToken);
        var recent = (await patients.ListRecentOrdersAsync(id, PageQuery.MaxPageSize, cancellationToken))
            .Select(o => new OrderListItemDto(
                o.Id, o.Number, o.CreatedAt, o.PatientName, o.PatientPhone, o.TestsCount, o.Total, o.Paid, o.Status.ToString(), o.Currency))
            .ToList();
        return new PatientDetailsDto(patient, recent);
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

        // Soft delete never trips the FK, so the no-orders rule is enforced in a serializable transaction
        // (a concurrent create cannot sneak an order in between the check and the delete).
        try
        {
            await patients.SoftDeleteIfNoOrdersAsync(patient, cancellationToken);
        }
        catch (AppException ex) when (ex.Code == ErrorCodes.PatientHasOrders)
        {
            await LogAsync(ActivityActions.PatientDeleteRefused, "Refused to delete a patient who has orders", patient, cancellationToken);
            throw;
        }

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
        p.Id, p.FullName, p.Phone, Notes: null, p.Gender.ToString(), p.DateOfBirth,
        p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version, p.HasUnpaidOrders);

    private static PatientDto ToExportDto(PatientExportRow p) => new(
        p.Id, p.FullName, p.Phone, Notes: null, p.Gender.ToString(), p.DateOfBirth,
        default, null, null, 0);

    private static PatientDto ToDto(Patient p) => new(
        p.Id, p.FullName, p.Phone, p.Notes, p.Gender.ToString(), p.DateOfBirth,
        p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version);
}
