using System.Security.Cryptography;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.LabTests;

/// <summary>The clinic's test catalog (Admin portal). Failures throw <see cref="AppException"/>.</summary>
public interface ILabTestService
{
    Task<PagedResult<LabTestDto>> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <see cref="PageQuery.MaxExportRows"/> tests for print/export.
    /// When <paramref name="active"/> is set, only that IsActive value is returned.
    /// </summary>
    Task<IReadOnlyList<LabTestDto>> ListAllAsync(bool? active, CancellationToken cancellationToken);

    Task<LabTestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<LabTestDto> CreateAsync(CreateLabTestRequest request, CancellationToken cancellationToken);

    Task<LabTestDto> UpdateAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Imports Name/Price rows from a CSV or Excel stream. Matching names update the existing lab cost
    /// (code and active stay); bad rows and later duplicates in the same file are skipped.
    /// </summary>
    Task<ImportLabTestsResult> ImportAsync(Stream stream, string? fileName, CancellationToken cancellationToken);
}

public class LabTestService(ILabTestRepository tests, IActivityLogger activity) : ILabTestService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int GeneratedCodeLength = 8;

    public async Task<PagedResult<LabTestDto>> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken)
    {
        var (items, total) = await tests.GetPageAsync(query, active, cancellationToken);
        return new PagedResult<LabTestDto>(items.Select(t => ToListDto(t)).ToList(), query.SafePage, query.SafePageSize, total);
    }

    public async Task<IReadOnlyList<LabTestDto>> ListAllAsync(bool? active, CancellationToken cancellationToken) =>
        (await tests.ListAllAsync(active, cancellationToken)).Select(ToExportDto).ToList();

    public async Task<LabTestDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToListDto(await tests.GetRowByIdAsync(id, cancellationToken) ?? throw AppException.NotFound());

    public async Task<LabTestDto> CreateAsync(CreateLabTestRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Code, request.Name, null, cancellationToken);

        var test = new LabTest(request.Code, request.Name, SearchText.Normalize(request.Name), request.LabCost!.Value);
        tests.Add(test);
        await SaveUniqueAsync(request.Code, request.Name, null, cancellationToken);

        await LogAsync(ActivityActions.LabTestCreated, "Created a lab test", test, cancellationToken);
        return ToDto(test);
    }

    public async Task<LabTestDto> UpdateAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken)
    {
        var test = await FindAsync(id, cancellationToken);
        if (test.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        await EnsureUniqueAsync(request.Code, request.Name, test.Id, cancellationToken);

        test.Update(
            request.Code, request.Name, SearchText.Normalize(request.Name), request.LabCost!.Value, request.IsActive);
        await SaveUniqueAsync(request.Code, request.Name, test.Id, cancellationToken);

        await LogAsync(ActivityActions.LabTestUpdated, "Updated a lab test", test, cancellationToken);
        return ToDto(test);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var (test, isUsed) = await tests.GetWithUsageAsync(id, cancellationToken);
        if (test is null)
        {
            throw AppException.NotFound();
        }

        if (isUsed)
        {
            throw AppException.Conflict(ErrorCodes.InUse);
        }

        tests.Remove(test);
        await tests.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.LabTestDeleted, "Deleted a lab test", test, cancellationToken);
    }

    public async Task<ImportLabTestsResult> ImportAsync(Stream stream, string? fileName, CancellationToken cancellationToken)
    {
        var rawRows = LabTestImportFile.ReadRows(stream, fileName);
        var skipped = 0;
        var candidates = new List<(int Row, string Name, string SearchName, long LabCost)>(rawRows.Count);
        var namesInFile = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rawRows)
        {
            var name = row.Name?.Trim() ?? string.Empty;
            var priceText = row.PriceText?.Trim() ?? string.Empty;
            if (name.Length == 0 || priceText.Length == 0
                || name.Length > LabTestRules.NameMaxLength
                || !LabTestImportFile.TryParsePrice(priceText, out var labCost))
            {
                skipped++;
                continue;
            }

            var searchName = SearchText.Normalize(name);
            if (!namesInFile.Add(searchName))
            {
                skipped++;
                continue;
            }

            candidates.Add((row.RowNumber, name, searchName, labCost));
        }

        var existing = await tests.GetTrackedBySearchNamesAsync(
            candidates.Select(c => c.SearchName).ToList(), cancellationToken);
        var bySearchName = existing.ToDictionary(t => t.SearchName, StringComparer.Ordinal);

        var toCreate = new List<(int Row, string Name, string SearchName, long LabCost)>(candidates.Count);
        var updated = 0;
        foreach (var candidate in candidates)
        {
            if (bySearchName.TryGetValue(candidate.SearchName, out var test))
            {
                ApplyImportUpdate(test, candidate.Name, candidate.SearchName, candidate.LabCost);
                updated++;
            }
            else
            {
                toCreate.Add(candidate);
            }
        }

        if (updated > 0)
        {
            await tests.SaveChangesAsync(cancellationToken);
        }

        var imported = 0;
        if (toCreate.Count > 0)
        {
            imported = await SaveImportAsync(
                toCreate,
                onUpdated: count => updated += count,
                cancellationToken);
        }

        if (imported == 0 && updated == 0)
        {
            return new ImportLabTestsResult(0, 0, skipped);
        }

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.LabTestsImported, ActivityModules.LabTests, "Imported lab tests")
            {
                Details = new { Imported = imported, Updated = updated, Skipped = skipped },
            },
            cancellationToken);

        return new ImportLabTestsResult(imported, updated, skipped);
    }

    /// <summary>
    /// Inserts new rows. On a unique-index race for a name, updates that existing test instead; code collisions retry.
    /// </summary>
    private async Task<int> SaveImportAsync(
        List<(int Row, string Name, string SearchName, long LabCost)> accepted,
        Action<int> onUpdated,
        CancellationToken cancellationToken)
    {
        var created = 0;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (accepted.Count == 0)
            {
                return created;
            }

            var codes = await AllocateCodesAsync(accepted.Count, cancellationToken);
            var entities = new List<LabTest>(accepted.Count);
            for (var i = 0; i < accepted.Count; i++)
            {
                var row = accepted[i];
                entities.Add(new LabTest(codes[i], row.Name, row.SearchName, row.LabCost));
            }

            tests.AddRange(entities);
            try
            {
                await tests.SaveChangesAsync(cancellationToken);
                return created + entities.Count;
            }
            catch (AppException ex) when (ex.Kind == AppErrorKind.Conflict && ex.Code == ErrorCodes.Taken)
            {
                tests.DiscardChanges();
                var taken = await tests.GetTrackedBySearchNamesAsync(
                    accepted.Select(a => a.SearchName).ToList(), cancellationToken);
                var bySearchName = taken.ToDictionary(t => t.SearchName, StringComparer.Ordinal);
                var remaining = new List<(int Row, string Name, string SearchName, long LabCost)>(accepted.Count);
                var racedUpdates = 0;
                foreach (var row in accepted)
                {
                    if (bySearchName.TryGetValue(row.SearchName, out var test))
                    {
                        ApplyImportUpdate(test, row.Name, row.SearchName, row.LabCost);
                        racedUpdates++;
                    }
                    else
                    {
                        // Name still free → likely a generated-code collision; retry with new codes.
                        remaining.Add(row);
                    }
                }

                if (racedUpdates > 0)
                {
                    await tests.SaveChangesAsync(cancellationToken);
                    onUpdated(racedUpdates);
                }

                accepted = remaining;
            }
        }

        throw AppException.Conflict(ErrorCodes.Taken);
    }

    private static void ApplyImportUpdate(LabTest test, string name, string searchName, long labCost) =>
        test.Update(test.Code, name, searchName, labCost, test.IsActive);

    private async Task<IReadOnlyList<string>> AllocateCodesAsync(int count, CancellationToken cancellationToken)
    {
        var codes = new List<string>(count);
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        // A few extras cover rare collisions with existing codes without another round trip.
        while (codes.Count < count)
        {
            var batch = new List<string>(count - codes.Count);
            while (batch.Count < count - codes.Count)
            {
                var code = NewCode();
                if (reserved.Add(code))
                {
                    batch.Add(code);
                }
            }

            var taken = await tests.FindExistingCodesAsync(batch, cancellationToken);
            foreach (var code in batch)
            {
                if (!taken.Contains(code))
                {
                    codes.Add(code);
                    if (codes.Count == count)
                    {
                        break;
                    }
                }
                else
                {
                    reserved.Remove(code);
                }
            }
        }

        return codes;
    }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[GeneratedCodeLength];
        Span<byte> bytes = stackalloc byte[GeneratedCodeLength];
        RandomNumberGenerator.Fill(bytes);
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        }

        return new string(chars);
    }

    private async Task<LabTest> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await tests.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();

    /// <summary>
    /// Saves, and if another request won the unique-index race after <see cref="EnsureUniqueAsync"/>, remaps the
    /// generic <see cref="ErrorCodes.Taken"/> from SaveChanges onto Code or Name.
    /// </summary>
    private async Task SaveUniqueAsync(string code, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        try
        {
            await tests.SaveChangesAsync(cancellationToken);
        }
        catch (AppException ex) when (ex.Kind == AppErrorKind.Conflict && ex.Code == ErrorCodes.Taken)
        {
            await EnsureUniqueAsync(code, name, exceptId, cancellationToken);
            throw;
        }
    }

    private async Task EnsureUniqueAsync(string code, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var (codeTaken, nameTaken) = await tests.FindDuplicatesAsync(
            LabTest.NormalizeCode(code), SearchText.Normalize(name), exceptId, cancellationToken);
        if (codeTaken)
        {
            throw AppException.Conflict(ErrorCodes.Taken, nameof(CreateLabTestRequest.Code));
        }

        if (nameTaken)
        {
            throw AppException.Conflict(ErrorCodes.Taken, nameof(CreateLabTestRequest.Name));
        }
    }

    private Task LogAsync(string action, string description, LabTest test, CancellationToken cancellationToken) =>
        activity.LogAsync(
            new ActivityEntry(action, ActivityModules.LabTests, description)
            {
                EntityType = nameof(LabTest),
                EntityId = test.Id.ToString(),
                Details = new { test.Code, test.Name, test.LabCost, test.IsActive },
            },
            cancellationToken);

    private static LabTestDto ToListDto(LabTestListRow t) => new(
        t.Id, t.Code, t.Name, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version);

    private static LabTestDto ToExportDto(LabTestExportRow t) => new(
        t.Id, t.Code, t.Name, t.LabCost, t.IsActive, default, null, null, 0);

    private static LabTestDto ToDto(LabTest t) => new(
        t.Id, t.Code, t.Name, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version);
}
