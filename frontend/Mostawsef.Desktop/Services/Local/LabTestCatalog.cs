using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// The active lab tests for New Order, downloaded once and kept until <see cref="LabTestCatalogVersion"/> changes
/// (a test was added, edited or deleted) or the user signs out. New Order searches this list in memory,
/// so opening the page again or typing a search sends no request.
/// </summary>
public sealed class LabTestCatalog
{
    private readonly LabTestsApi _api;
    private readonly LabTestCatalogVersion _version;
    private IReadOnlyList<LabTestDto>? _tests;
    private int _loadedVersion;

    public LabTestCatalog(LabTestsApi api, LabTestCatalogVersion version, SessionService session)
    {
        _api = api;
        _version = version;
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                _tests = null;
            }
        };
    }

    /// <summary>The cached tests, or downloads them when there are none or the catalog changed since.</summary>
    public async Task<ApiResult<IReadOnlyList<LabTestDto>>> GetAsync(CancellationToken cancellationToken)
    {
        // Read the version before the download: a change made while it runs makes the result stale at once.
        var version = _version.Current;
        if (_tests is { } cached && _loadedVersion == version)
        {
            return ApiResult<IReadOnlyList<LabTestDto>>.Ok(cached);
        }

        var result = await _api.GetCatalogAsync(search: null, cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResult<IReadOnlyList<LabTestDto>>.Fail(result.Error, result.StatusCode, result.Problem);
        }

        _tests = result.Value!.Items;
        _loadedVersion = version;
        return ApiResult<IReadOnlyList<LabTestDto>>.Ok(_tests);
    }
}
