using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>The lab test catalog (Admin only).</summary>
public class LabTestsApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<LabTestDto>>> GetTestsAsync(
        int page,
        int pageSize,
        string? search,
        bool? active = null,
        bool includeTotal = true,
        CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<LabTestDto>>(
            $"api/lab-tests?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}") +
            (active is { } isActive ? $"&active={isActive.ToString().ToLowerInvariant()}" : string.Empty),
            cancellationToken);

    /// <summary>
    /// Active tests for the new-order catalog, optionally filtered by search.
    /// Walks every page (MaxPageSize each) so a clinic catalog larger than one page is not truncated.
    /// </summary>
    public async Task<ApiResult<PagedResult<LabTestDto>>> GetCatalogAsync(string? search, CancellationToken cancellationToken = default)
    {
        var all = new List<LabTestDto>();
        var page = 1;
        while (true)
        {
            var result = await GetTestsAsync(
                page, PageQuery.MaxPageSize, search, active: true, includeTotal: false, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            var batch = result.Value!.Items;
            all.AddRange(batch);
            if (batch.Count < PageQuery.MaxPageSize)
            {
                return ApiResult<PagedResult<LabTestDto>>.Ok(
                    new PagedResult<LabTestDto>(all, 1, all.Count == 0 ? PageQuery.MaxPageSize : all.Count, all.Count));
            }

            page++;
        }
    }

    /// <summary>Every test, sorted by name.</summary>
    public Task<ApiResult<IReadOnlyList<LabTestDto>>> GetAllTestsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<LabTestDto>>("api/lab-tests/all", cancellationToken);

    public Task<ApiResult<LabTestDto>> GetTestAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<LabTestDto>($"api/lab-tests/{id}", cancellationToken);

    public Task<ApiResult<LabTestDto>> CreateTestAsync(CreateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateLabTestRequest, LabTestDto>("api/lab-tests", request, cancellationToken);

    public Task<ApiResult<LabTestDto>> UpdateTestAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateLabTestRequest, LabTestDto>($"api/lab-tests/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> DeleteTestAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/lab-tests/{id}", cancellationToken);
}
