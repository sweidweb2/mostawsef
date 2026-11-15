using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Base of the Admin and Super dashboards: the period filter, one request per load (a newer load cancels the older
/// one), the loading and error states, and Refresh. A filter change loads at once unless the server already answered
/// for exactly that period (e.g. it only moved because the server's bounds arrived).
/// </summary>
public abstract partial class DashboardPageViewModel : ViewModelBase
{
    private readonly ErrorReporter _errors;
    private CancellationTokenSource? _loadCancellation;

    protected DashboardPageViewModel(ErrorReporter errors)
    {
        _errors = errors;
        Filter = new DashboardPeriodFilter(DateOnly.FromDateTime(DateTime.Now));
        Filter.Changed += (_, _) =>
        {
            if (ShownRange is not { } shown || shown.From != Filter.From || shown.To != Filter.To)
            {
                _ = LoadSafelyAsync();
            }
            else
            {
                // Back on the period already shown: drop a load still running for the period chosen in between.
                _loadCancellation?.Cancel();
            }
        };
    }

    public DashboardPeriodFilter Filter { get; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    /// <summary>The period the figures on screen cover, as the server resolved it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BucketKey), nameof(Unit), nameof(IsSingleDay))]
    public partial DashboardRangeDto? ShownRange { get; private set; }

    /// <summary>"Dashboard.Bucket.Day": how the graphs are split, translated in the view.</summary>
    public string BucketKey => "Dashboard.Bucket." + (ShownRange?.Bucket ?? DashboardBuckets.Day);

    public ChartUnit Unit => DashboardPeriodFilter.UnitOf(ShownRange?.Bucket);

    /// <summary>The shown period is one day: the caption shows a single date.</summary>
    public bool IsSingleDay => ShownRange is { } range && range.From == range.To;

    public virtual void Deactivate()
    {
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    /// <summary>
    /// Asks the server for <paramref name="from"/>..<paramref name="to"/> and shows the answer. Returns the period the
    /// server resolved, or null when the request failed (nothing shown changes then). Throws when cancelled.
    /// </summary>
    protected abstract Task<DashboardRangeDto?> FetchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    protected async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = true;
        try
        {
            var range = await FetchAsync(Filter.From, Filter.To, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            HasLoadError = range is null;
            if (range is not null)
            {
                ShownRange = range;
                Filter.ApplyBounds(range.FirstDate, range.Today);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page or chose another period; that load replaces this one.
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }

    private async Task LoadSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Loading the dashboard");
        }
    }
}
