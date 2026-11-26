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
    /// Active tests for the new-order catalog, sorted by name. One request (the same list as print/export, filtered
    /// here) so the catalog is a consistent snapshot: walking pages could skip or repeat a test edited in between.
    /// </summary>
    public async Task<ApiResult<IReadOnlyList<LabTestDto>>> GetActiveCatalogAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAllTestsAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return result;
        }

        var active = new List<LabTestDto>(result.Value!.Count);
        foreach (var test in result.Value)
        {
            if (test.IsActive)
            {
                active.Add(test);
            }
        }

        return ApiResult<IReadOnlyList<LabTestDto>>.Ok(active);
    }

    /// <summary>Every test, sorted by name.</summary>
    public Task<ApiResult<IReadOnlyList<LabTestDto>>> GetAllTestsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<LabTestDto>>("api/lab-tests/all", cancellationToken);

    public Task<ApiResult<LabTestDto>> CreateTestAsync(CreateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateLabTestRequest, LabTestDto>("api/lab-tests", request, cancellationToken);

    public Task<ApiResult<LabTestDto>> UpdateTestAsync(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateLabTestRequest, LabTestDto>($"api/lab-tests/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> DeleteTestAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/lab-tests/{id}", cancellationToken);

    /// <summary>Uploads a CSV Name/Price file; the server generates codes and skips bad rows.</summary>
    public Task<ApiResult<ImportLabTestsResult>> ImportTestsAsync(string filePath, CancellationToken cancellationToken = default) =>
        api.PostFileAsync<ImportLabTestsResult>("api/lab-tests/import", filePath, cancellationToken: cancellationToken);

    /// <summary>Downloads the CSV import template to <paramref name="destinationPath"/>.</summary>
    public Task<ApiResult<bool>> DownloadImportTemplateAsync(
        string destinationPath, CancellationToken cancellationToken = default) =>
        api.DownloadAsync("api/lab-tests/import-template", destinationPath, cancellationToken);
}
