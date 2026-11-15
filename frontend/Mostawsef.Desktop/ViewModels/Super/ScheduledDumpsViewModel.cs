using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Automatic dump history hosted on the Super Backups page (when, where, retention settings).</summary>
public partial class ScheduledDumpsViewModel : NumberedListViewModel<ScheduledDumpDto>
{
    private readonly ScheduledDumpsApi _api;

    public ScheduledDumpsViewModel(
        ScheduledDumpsApi api,
        IToastService toasts,
        LocalizationService localization,
        ErrorReporter errors)
        : base(toasts, localization, errors)
    {
        _api = api;
    }

    protected override Task LoadOnActivateAsync() => ReloadAsync();

    protected override Task<ApiResult<PagedResult<ScheduledDumpDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        _api.GetPageAsync(page, pageSize, includeTotal, cancellationToken);
}
