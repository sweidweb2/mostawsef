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

    public Task<LabTestListRow?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.LabTests.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new LabTestListRow(
                t.Id, t.Code, t.Name, t.Price, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(LabTest? Test, bool IsUsed)> GetWithUsageAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.LabTests
            .Where(t => t.Id == id)
            .Select(t => new { Test = t, IsUsed = db.OrderItems.Any(i => i.LabTestId == t.Id) })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? (null, false) : (row.Test, row.IsUsed);
    }

    public async Task<IReadOnlyList<LabTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.LabTests.Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);

    public async Task<(bool CodeTaken, bool NameTaken)> FindDuplicatesAsync(
        string code, string searchName, Guid? exceptId, CancellationToken cancellationToken)
    {
        var hits = await db.LabTests.AsNoTracking()
            .Where(t => t.Id != exceptId && (t.Code == code || t.SearchName == searchName))
            .Select(t => new { CodeHit = t.Code == code, NameHit = t.SearchName == searchName })
            .ToListAsync(cancellationToken);
        return (hits.Exists(h => h.CodeHit), hits.Exists(h => h.NameHit));
    }

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
