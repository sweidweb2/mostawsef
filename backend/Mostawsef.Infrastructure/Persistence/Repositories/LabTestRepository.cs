using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class LabTestRepository(AppDbContext db) : ILabTestRepository
{
    public Task<LabTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.LabTests.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<LabTestDto?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.LabTests.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new LabTestDto(
                t.Id, t.Code, t.Name, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version))
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
        await db.LabTests.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(cancellationToken);

    public async Task<(bool CodeTaken, bool NameTaken)> FindDuplicatesAsync(
        string code, string searchName, Guid? exceptId, CancellationToken cancellationToken)
    {
        var hits = await db.LabTests.AsNoTracking()
            .Where(t => t.Id != exceptId && (t.Code == code || t.SearchName == searchName))
            .Select(t => new { CodeHit = t.Code == code, NameHit = t.SearchName == searchName })
            .ToListAsync(cancellationToken);
        return (hits.Exists(h => h.CodeHit), hits.Exists(h => h.NameHit));
    }

    public async Task<HashSet<string>> FindExistingSearchNamesAsync(
        IReadOnlyCollection<string> searchNames, CancellationToken cancellationToken)
    {
        if (searchNames.Count == 0)
        {
            return [];
        }

        var hits = await db.LabTests.AsNoTracking()
            .Where(t => searchNames.Contains(t.SearchName))
            .Select(t => t.SearchName)
            .ToListAsync(cancellationToken);
        return hits.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<LabTest>> GetTrackedBySearchNamesAsync(
        IReadOnlyCollection<string> searchNames, CancellationToken cancellationToken)
    {
        if (searchNames.Count == 0)
        {
            return [];
        }

        return await db.LabTests
            .Where(t => searchNames.Contains(t.SearchName))
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<string>> FindExistingCodesAsync(
        IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        var hits = await db.LabTests.AsNoTracking()
            .Where(t => codes.Contains(t.Code))
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);
        return hits.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<(IReadOnlyList<LabTestDto> Items, int Total)> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken)
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
            .Select(t => new LabTestDto(
                t.Id, t.Code, t.Name, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version));

        return await rows.ToPageAsync(tests, query, cancellationToken);
    }

    public async Task<IReadOnlyList<LabTestDto>> ListAllAsync(bool? active, CancellationToken cancellationToken)
    {
        var tests = db.LabTests.AsNoTracking();
        if (active is { } isActive)
        {
            tests = tests.Where(t => t.IsActive == isActive);
        }

        return await tests
            .OrderBy(t => t.SearchName)
            .ThenBy(t => t.Code)
            .Take(PageQuery.MaxExportRows)
            .Select(t => new LabTestDto(t.Id, t.Code, t.Name, t.LabCost, t.IsActive, default, null, null, 0))
            .ToListAsync(cancellationToken);
    }

    public void Add(LabTest test) => db.LabTests.Add(test);

    public void AddRange(IEnumerable<LabTest> tests) => db.LabTests.AddRange(tests);

    public void Remove(LabTest test) => db.LabTests.Remove(test);

    public void DiscardChanges() => db.ChangeTracker.Clear();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
