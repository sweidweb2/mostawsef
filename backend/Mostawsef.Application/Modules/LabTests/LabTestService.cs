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

    /// <summary>Up to <see cref="PageQuery.MaxExportRows"/> tests for print/export.</summary>
    Task<IReadOnlyList<LabTestDto>> ListAllAsync(CancellationToken cancellationToken);

    Task<LabTestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<LabTestDto> CreateAsync(CreateLabTestRequest request, CancellationToken cancellationToken);

    Task<LabTestDto> UpdateAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class LabTestService(ILabTestRepository tests, IActivityLogger activity) : ILabTestService
{
    public async Task<PagedResult<LabTestDto>> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken)
    {
        var (items, total) = await tests.GetPageAsync(query, active, cancellationToken);
        return new PagedResult<LabTestDto>(items.Select(t => ToListDto(t)).ToList(), query.SafePage, query.SafePageSize, total);
    }

    public async Task<IReadOnlyList<LabTestDto>> ListAllAsync(CancellationToken cancellationToken) =>
        (await tests.ListAllAsync(cancellationToken)).Select(ToExportDto).ToList();

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
