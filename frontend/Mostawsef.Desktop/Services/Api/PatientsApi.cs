using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Patient records (Admin only).</summary>
public class PatientsApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<PatientDto>>> GetPatientsAsync(
        int page, int pageSize, string? search, bool includeTotal = true, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<PatientDto>>(
            $"api/patients?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}"),
            cancellationToken);

    /// <summary>One page of patients matching <paramref name="search"/> for the new-order autocomplete.</summary>
    public Task<ApiResult<PagedResult<PatientDto>>> SearchPatientsAsync(
        string? search, CancellationToken cancellationToken = default) =>
        GetPatientsAsync(1, PageQuery.DefaultPageSize, search, includeTotal: false, cancellationToken);

    /// <summary>Every patient, sorted by name (print/export).</summary>
    public Task<ApiResult<IReadOnlyList<PatientDto>>> GetAllPatientsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<PatientDto>>("api/patients/all", cancellationToken);

    public Task<ApiResult<PatientDto>> CreatePatientAsync(CreatePatientRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreatePatientRequest, PatientDto>("api/patients", request, cancellationToken);

    public Task<ApiResult<PatientDto>> UpdatePatientAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdatePatientRequest, PatientDto>($"api/patients/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> DeletePatientAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/patients/{id}", cancellationToken);
}
