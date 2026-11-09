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

    public async Task<(IReadOnlyList<LabTestListRow> Items, int Total)> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken)
    {
        var tests = db.LabTests.AsNoTracking();
        if (active is { } isActive)
        {
            tests = tests.Where(t => t.IsActive == isActive);
        }

        var term = SearchText.Normalize(query.SafeSearch);
        if (term.Length > 0)
        {
            // Prefix match so the unique B-trees on SearchName / Code can be used (catalogs stay small either way).
            var code = LabTest.NormalizeCode(term);
            tests = tests.Where(t => t.SearchName.StartsWith(term) || t.Code.StartsWith(code));
        }

        var rows = tests
            .OrderBy(t => t.SearchName)
            .ThenBy(t => t.Code)
            .Select(t => new LabTestListRow(
                t.Id, t.Code, t.Name, t.Price, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version));

        return await rows.ToPageAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<LabTestListRow>> ListAllAsync(CancellationToken cancellationToken) =>
        await db.LabTests.AsNoTracking()
            .OrderBy(t => t.SearchName)
            .ThenBy(t => t.Code)
            .Select(t => new LabTestListRow(
                t.Id, t.Code, t.Name, t.Price, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version))
            .ToListAsync(cancellationToken);

    public void Add(LabTest test) => db.LabTests.Add(test);

    public void Remove(LabTest test) => db.LabTests.Remove(test);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
