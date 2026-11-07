using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class LabTestRepository(AppDbContext db) : ILabTestRepository
{
    public Task<LabTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.LabTests.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LabTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.LabTests.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);

    public Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        db.OrderItems.AnyAsync(i => i.LabTestId == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.LabTests.AnyAsync(t => t.Code == code && t.Id != exceptId, cancellationToken);

    public Task<bool> NameExistsAsync(string searchName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.LabTests.AnyAsync(t => t.SearchName == searchName && t.Id != exceptId, cancellationToken);

    public async Task<(IReadOnlyList<LabTest> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var tests = db.LabTests.AsNoTracking();

        var term = SearchText.Normalize(query.SafeSearch);
        if (term.Length > 0)
        {
            var code = LabTest.NormalizeCode(term);
            tests = tests.Where(t => t.SearchName.Contains(term) || t.Code.Contains(code));
        }

        var total = await tests.CountAsync(cancellationToken);
        var items = await tests
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Code)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(LabTest test) => db.LabTests.Add(test);

    public void Remove(LabTest test) => db.LabTests.Remove(test);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
