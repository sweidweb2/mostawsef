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
    public Task<ApiResult<PagedResult<LabTestDto>>> GetTestsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<LabTestDto>>(
            $"api/lab-tests?page={page}&pageSize={pageSize}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}"),
            cancellationToken);

    /// <summary>Every test, sorted by name, read page by page at the largest page size. Fails as soon as one page fails.</summary>
    public async Task<ApiResult<IReadOnlyList<LabTestDto>>> GetAllTestsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<LabTestDto>();
        for (var page = 1; ; page++)
        {
            var result = await GetTestsAsync(page, PageQuery.MaxPageSize, null, cancellationToken);
            if (!result.IsSuccess)
            {
                return ApiResult<IReadOnlyList<LabTestDto>>.Fail(result.Error, result.StatusCode, result.Problem);
            }

            all.AddRange(result.Value!.Items);
            if (result.Value.Items.Count == 0 || page >= result.Value.TotalPages)
            {
                return ApiResult<IReadOnlyList<LabTestDto>>.Ok(all);
            }
        }
    }

    public Task<ApiResult<LabTestDto>> GetTestAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<LabTestDto>($"api/lab-tests/{id}", cancellationToken);

    public Task<ApiResult<LabTestDto>> CreateTestAsync(CreateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateLabTestRequest, LabTestDto>("api/lab-tests", request, cancellationToken);

    public Task<ApiResult<LabTestDto>> UpdateTestAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateLabTestRequest, LabTestDto>($"api/lab-tests/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> DeleteTestAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/lab-tests/{id}", cancellationToken);
}
