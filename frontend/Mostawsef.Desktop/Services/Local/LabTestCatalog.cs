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
    private readonly object _gate = new();
    private IReadOnlyList<LabTestDto>? _tests;
    private int _loadedVersion;

    /// <summary>Bumped on sign-out so a download that finishes afterwards is not kept for the next session.</summary>
    private int _epoch;

    public LabTestCatalog(LabTestsApi api, LabTestCatalogVersion version, SessionService session)
    {
        _api = api;
        _version = version;
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                lock (_gate)
                {
                    _epoch++;
                    _tests = null;
                }
            }
        };
    }

    /// <summary>The cached tests, or downloads them when there are none or the catalog changed since.</summary>
    public async Task<ApiResult<IReadOnlyList<LabTestDto>>> GetAsync(CancellationToken cancellationToken)
    {
        // Read the version before the download: a change made while it runs makes the result stale at once.
        int version;
        int epoch;
        lock (_gate)
        {
            version = _version.Current;
            epoch = _epoch;
            if (_tests is { } cached && _loadedVersion == version)
            {
                return ApiResult<IReadOnlyList<LabTestDto>>.Ok(cached);
            }
        }

        var result = await _api.GetActiveCatalogAsync(cancellationToken);
        if (!result.IsSuccess)
        {
            return result;
        }

        lock (_gate)
        {
            if (epoch == _epoch)
            {
                _tests = result.Value;
                _loadedVersion = version;
            }
        }

        return result;
    }
}
