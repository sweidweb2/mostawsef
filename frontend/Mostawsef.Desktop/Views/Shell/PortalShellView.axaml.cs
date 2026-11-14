using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Views.Shell;

public partial class PortalShellView : UserControl
{
    /// <summary>
    /// Builds the other menu pages' views in idle time after sign-in, so their first visit is instant instead of
    /// 200-500 ms. On in the app (App.OnFrameworkInitializationCompleted); off in tests unless a test turns it on.
    /// </summary>
    public static bool PrebuildMenuPages { get; set; }

    /// <summary>Wait after the portal appears, so the first page loads and paints before any pre-building.</summary>
    public static TimeSpan PrebuildDelay { get; set; } = TimeSpan.FromSeconds(1);

    private PortalShellViewModel? _shell;
    private PageHost? _pageHost;
    private CancellationTokenSource? _prebuild;

    public PortalShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            _pageHost ??= this.FindControl<PageHost>("PageHost");
            StartPrebuild();
        };
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _shell = DataContext as PortalShellViewModel;
        if (_shell is null)
        {
            return;
        }

        _shell.PageDiscarded += OnPageDiscarded;
        StartPrebuild();
    }

    private void Unsubscribe()
    {
        _prebuild?.Cancel();
        _prebuild = null;

        if (_shell is not null)
        {
            _shell.PageDiscarded -= OnPageDiscarded;
            _shell = null;
        }
    }

    private void OnPageDiscarded(ViewModelBase page)
    {
        _pageHost ??= this.FindControl<PageHost>("PageHost");
        _pageHost?.Release(page);
    }

    private void StartPrebuild()
    {
        if (!PrebuildMenuPages || _prebuild is not null || _shell is not { } shell || _pageHost is not { } host || VisualRoot is null)
        {
            return;
        }

        var prebuild = new CancellationTokenSource();
        _prebuild = prebuild;
        _ = PrebuildAsync(shell, host, prebuild.Token);
    }

    /// <summary>
    /// One page per dispatcher job at Background priority: clicks and typing always run first, and at most one page
    /// build (one job) stands between the user and the next input. A failure is only traced: the same view would fail
    /// again when opened, and that path reports it.
    /// </summary>
    private static async Task PrebuildAsync(PortalShellViewModel shell, PageHost host, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PrebuildDelay, cancellationToken);
            var retries = 0;
            for (var i = 0; i < shell.MenuItems.Count; i++)
            {
                var index = i;
                var built = await Dispatcher.UIThread.InvokeAsync(
                    () => cancellationToken.IsCancellationRequested
                        || shell.GetOrCreateMenuPage(index) is not { } page
                        || host.Prebuild(page),
                    DispatcherPriority.Background,
                    cancellationToken);

                if (!built)
                {
                    // Not laid out yet (no size to build at): wait a little and try this page again, for up to ~10 s.
                    if (++retries > 100)
                    {
                        return;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                    i--;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Signed out or the portal closed.
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Pre-building menu pages failed: {0}", ex);
        }
    }
}
