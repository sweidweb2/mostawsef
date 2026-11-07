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

    public async Task<(IReadOnlyList<Patient> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var patients = db.Patients.AsNoTracking();

        var term = SearchText.Normalize(query.SafeSearch);
        if (term.Length > 0)
        {
            patients = patients.Where(p =>
                p.SearchName.Contains(term) || p.Phone.Contains(term) || (p.GuardianPhone != null && p.GuardianPhone.Contains(term)));
        }

        var total = await patients.CountAsync(cancellationToken);
        var items = await patients
            .OrderBy(p => p.FullName)
            .ThenBy(p => p.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(Patient patient) => db.Patients.Add(patient);

    public void Remove(Patient patient) => db.Patients.Remove(patient);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
