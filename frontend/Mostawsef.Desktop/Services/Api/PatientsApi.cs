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

    /// <summary>Every patient, sorted by name.</summary>
    public Task<ApiResult<IReadOnlyList<PatientDto>>> GetAllPatientsAsync(CancellationToken cancellationToken = default) =>
        api.GetAllPagesAsync<PatientDto>("api/patients", cancellationToken);

    public Task<ApiResult<PatientDto>> GetPatientAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<PatientDto>($"api/patients/{id}", cancellationToken);

    public Task<ApiResult<PatientDto>> CreatePatientAsync(CreatePatientRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreatePatientRequest, PatientDto>("api/patients", request, cancellationToken);

    public Task<ApiResult<PatientDto>> UpdatePatientAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdatePatientRequest, PatientDto>($"api/patients/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> DeletePatientAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/patients/{id}", cancellationToken);
}
