using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
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

    public async Task<(IReadOnlyList<PatientListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var patients = db.Patients.AsNoTracking();

        if (TrigramSearch.ContainsPattern(query.SafeSearch) is { } pattern)
        {
            patients = patients.Where(p => EF.Functions.Like(p.SearchText, pattern, TrigramSearch.EscapeCharacter));
        }

        var rows = patients
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Select(p => new PatientListRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.IsInsured, p.Gender, p.DateOfBirth,
                p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version));

        return await rows.ToPageAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<PatientListRow>> ListAllAsync(CancellationToken cancellationToken) =>
        await db.Patients.AsNoTracking()
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Select(p => new PatientListRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.IsInsured, p.Gender, p.DateOfBirth,
                p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version))
            .ToListAsync(cancellationToken);

    public void Add(Patient patient) => db.Patients.Add(patient);

    public void Remove(Patient patient) => db.Patients.Remove(patient);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
