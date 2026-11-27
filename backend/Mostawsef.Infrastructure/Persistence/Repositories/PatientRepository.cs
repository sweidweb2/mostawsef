using System.Data;
using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class PatientRepository(AppDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PatientDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PatientDto(
                p.Id, p.FullName, p.Phone, p.Notes,
                p.Gender == Gender.Male ? Genders.Male : Genders.Female,
                p.DateOfBirth, p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOrdersAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(o => o.PatientId == id, cancellationToken);

    public async Task<IReadOnlyList<OrderListRow>> ListRecentOrdersAsync(
        Guid patientId, int take, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(take)
            .Select(o => new OrderListRow(
                o.Id,
                o.Number,
                o.CreatedAt,
                o.PatientName,
                o.PatientPhone,
                o.TestsCount,
                o.Total,
                o.Paid,
                o.Status,
                o.Currency))
            .ToListAsync(cancellationToken);

    public Task<OrderPatientRow?> GetOrderPatientAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new OrderPatientRow(p.Id, p.FullName, p.SearchName, p.Phone))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<PatientDto> Items, int Total)> GetPageAsync(PageQuery query, bool includeUnpaid, CancellationToken cancellationToken)
    {
        var patients = db.Patients.AsNoTracking();

        if (TrigramSearch.ContainsPattern(query.SafeSearch) is { } pattern)
        {
            patients = patients.Where(p => EF.Functions.Like(p.SearchText, pattern, TrigramSearch.EscapeCharacter));
        }

        // One projection: includeUnpaid is a query parameter, so the database only runs the EXISTS subquery when it is true
        // (CASE WHEN @includeUnpaid THEN EXISTS(...) ELSE NULL END); other callers do not pay for it.
        var rows = patients
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Select(p => new PatientDto(
                p.Id, p.FullName, p.Phone, Notes: null,
                p.Gender == Gender.Male ? Genders.Male : Genders.Female,
                p.DateOfBirth, p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version,
                includeUnpaid
                    ? db.Orders.Any(o => o.PatientId == p.Id
                        && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyPaid))
                    : (bool?)null));

        return await rows.ToPageAsync(patients, query, cancellationToken);
    }

    public async Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken) =>
        await db.Patients.AsNoTracking()
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Take(PageQuery.MaxExportRows)
            .Select(p => new PatientDto(
                p.Id, p.FullName, p.Phone, Notes: null,
                p.Gender == Gender.Male ? Genders.Male : Genders.Female,
                p.DateOfBirth, default, null, null, 0))
            .ToListAsync(cancellationToken);

    public void Add(Patient patient) => db.Patients.Add(patient);

    public void Remove(Patient patient) => db.Patients.Remove(patient);

    public Task SoftDeleteIfNoOrdersAsync(Patient patient, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // Same context as FindAsync: re-check orders under Serializable so a concurrent create cannot land between the check and the soft delete.
            if (await db.Orders.AnyAsync(o => o.PatientId == patient.Id, cancellationToken))
            {
                throw AppException.Conflict(ErrorCodes.PatientHasOrders);
            }

            db.Patients.Remove(patient);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
