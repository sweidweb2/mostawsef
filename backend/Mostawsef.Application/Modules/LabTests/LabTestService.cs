using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.LabTests;

/// <summary>The clinic's test catalog (Admin portal). Failures throw <see cref="AppException"/>.</summary>
public interface ILabTestService
{
    Task<PagedResult<LabTestDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    Task<LabTestDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<LabTestDto> CreateAsync(CreateLabTestRequest request, CancellationToken cancellationToken);

    Task<LabTestDto> UpdateAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class LabTestService(ILabTestRepository tests, IActivityLogger activity) : ILabTestService
{
    public async Task<PagedResult<LabTestDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await tests.GetPageAsync(query, cancellationToken);
        return new PagedResult<LabTestDto>(items.Select(ToDto).ToList(), query.SafePage, query.SafePageSize, total);
    }

    public async Task<LabTestDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await FindAsync(id, cancellationToken));

    public async Task<LabTestDto> CreateAsync(CreateLabTestRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueAsync(request.Code, request.Name, null, cancellationToken);

        var test = new LabTest(request.Code, request.Name, SearchText.Normalize(request.Name), request.Price!.Value, request.LabCost!.Value);
        tests.Add(test);
        await tests.SaveChangesAsync(cancellationToken);

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
            request.Code, request.Name, SearchText.Normalize(request.Name), request.Price!.Value, request.LabCost!.Value, request.IsActive);
        await tests.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.LabTestUpdated, "Updated a lab test", test, cancellationToken);
        return ToDto(test);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var test = await FindAsync(id, cancellationToken);
        if (await tests.IsUsedAsync(test.Id, cancellationToken))
        {
            throw AppException.Conflict(ErrorCodes.InUse);
        }

        tests.Remove(test);
        await tests.SaveChangesAsync(cancellationToken);

        await LogAsync(ActivityActions.LabTestDeleted, "Deleted a lab test", test, cancellationToken);
    }

    private async Task<LabTest> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await tests.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();

    private async Task EnsureUniqueAsync(string code, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await tests.CodeExistsAsync(LabTest.NormalizeCode(code), exceptId, cancellationToken))
        {
            throw AppException.Conflict(ErrorCodes.Taken, nameof(CreateLabTestRequest.Code));
        }

        if (await tests.NameExistsAsync(SearchText.Normalize(name), exceptId, cancellationToken))
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
                Details = new { test.Code, test.Name, test.Price, test.LabCost, test.IsActive },
            },
            cancellationToken);

    private static LabTestDto ToDto(LabTest t) => new(
        t.Id, t.Code, t.Name, t.Price, t.LabCost, t.IsActive, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Version);
}
