using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class PatientRepository(AppDbContext db) : IPatientRepository
{
    public Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PatientDetailsRow?> GetDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PatientDetailsRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.Notes, p.IsInsured, p.Gender, p.DateOfBirth,
                p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOrdersAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(o => o.PatientId == id, cancellationToken);

    public Task<OrderPatientRow?> GetOrderPatientAsync(Guid id, CancellationToken cancellationToken) =>
        db.Patients.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new OrderPatientRow(p.Id, p.FullName, p.SearchName, p.Phone))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<PatientListRow> Items, int Total)> GetPageAsync(PageQuery query, bool includeUnpaid, CancellationToken cancellationToken)
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
            .Select(p => new PatientListRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.IsInsured, p.Gender, p.DateOfBirth,
                p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version,
                includeUnpaid
                    ? db.Orders.Any(o => o.PatientId == p.Id
                        && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyPaid))
                    : (bool?)null));

        return await rows.ToPageAsync(patients, query, cancellationToken);
    }

    public async Task<IReadOnlyList<PatientExportRow>> ListAllAsync(CancellationToken cancellationToken) =>
        await db.Patients.AsNoTracking()
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Take(PageQuery.MaxExportRows)
            .Select(p => new PatientExportRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.IsInsured, p.Gender, p.DateOfBirth))
            .ToListAsync(cancellationToken);

    public void Add(Patient patient) => db.Patients.Add(patient);

    public void Remove(Patient patient) => db.Patients.Remove(patient);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
