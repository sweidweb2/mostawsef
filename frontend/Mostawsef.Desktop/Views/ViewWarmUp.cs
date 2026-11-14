using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Views;

/// <summary>
/// While the login screen is idle (the user is typing), builds each main view once in an invisible layer and drops it.
/// The first time a view is built the process pays for JIT compilation and first-time XAML/style work; doing that
/// here made the portal appear 3-4x faster after sign-in (≈800 ms → ≈250 ms on a cold start) and the first menu
/// pages 2-3x faster. One view per background dispatcher job, so typing always goes first; stops at sign-in.
/// </summary>
public static class ViewWarmUp
{
    /// <summary>Wait after the login screen appears, so it paints and takes focus first.</summary>
    public static TimeSpan StartDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    private static bool _done;

    /// <summary>Most likely first: what appears right after sign-in, then the Admin portal, then the Super portal.</summary>
    public static IReadOnlyList<Type> ViewModelTypes()
    {
        var types = new List<Type> { typeof(PortalShellViewModel) };
        foreach (var role in new[] { Roles.Admin, Roles.Super })
        {
            if (AppRoutes.Table.FindPortal(role) is { } portal)
            {
                types.AddRange(portal.Pages.Select(p => p.PageType));
            }

            if (role == Roles.Admin)
            {
                types.AddRange([typeof(NewOrderViewModel), typeof(OrderDetailsViewModel), typeof(PatientFormViewModel), typeof(LabTestFormViewModel)]);
            }
            else
            {
                types.AddRange([typeof(UserFormViewModel), typeof(ActivityLogDetailsViewModel), typeof(SystemErrorDetailsViewModel)]);
            }
        }

        types.Add(typeof(ChangePasswordFormViewModel));
        return types.Distinct().ToList();
    }

    /// <summary>Runs once per process. <paramref name="keepGoing"/> is checked before each view (false after sign-in).</summary>
    public static async Task RunAsync(Panel layer, Func<bool> keepGoing, CancellationToken cancellationToken = default)
    {
        if (_done)
        {
            return;
        }

        _done = true;
        try
        {
            await Task.Delay(StartDelay, cancellationToken);
            foreach (var type in ViewModelTypes())
            {
                var stop = await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        if (!keepGoing())
                        {
                            return true;
                        }

                        BuildOnce(layer, type);
                        return false;
                    },
                    DispatcherPriority.Background,
                    cancellationToken);

                if (stop)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // Only an optimization: the real view would fail the same way when opened, and that path reports it.
            Trace.TraceWarning("View warm-up failed: {0}", ex);
        }
        finally
        {
            layer.Children.Clear();
        }
    }

    internal static void BuildOnce(Panel layer, Type viewModelType)
    {
        if (AppRoutes.Table.CreateView(viewModelType) is not { } view)
        {
            return;
        }

        // No DataContext: templates, styles and layout code run (that is what JIT needs), bindings stay idle.
        var size = layer.Bounds.Size is { Width: > 0, Height: > 0 } bounds ? bounds : new Size(1280, 800);
        layer.Children.Add(view);
        view.Measure(size);
        view.Arrange(new Rect(size));
        layer.Children.Remove(view);
    }

    /// <summary>For tests: allow another run in the same process.</summary>
    internal static void Reset() => _done = false;
}
