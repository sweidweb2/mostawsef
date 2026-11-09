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

    public async Task<(IReadOnlyList<PatientListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var patients = db.Patients.AsNoTracking();

        if (TrigramSearch.ContainsPattern(query.SafeSearch) is { } pattern)
        {
            patients = patients.Where(p => EF.Functions.Like(p.SearchText, pattern, TrigramSearch.EscapeCharacter));
        }

        var total = query.IncludeTotal
            ? await patients.CountAsync(cancellationToken)
            : PageQuery.TotalNotComputed;
        var items = await patients
            .OrderBy(p => p.SearchName)
            .ThenBy(p => p.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(p => new PatientListRow(
                p.Id, p.FullName, p.Phone, p.GuardianPhone, p.IsInsured, p.Gender, p.DateOfBirth,
                p.CreatedAt, p.UpdatedAt, p.UpdatedBy, p.Version))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(Patient patient) => db.Patients.Add(patient);

    public void Remove(Patient patient) => db.Patients.Remove(patient);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
