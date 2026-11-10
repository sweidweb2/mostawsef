using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Shows the view of <see cref="Page"/> (a view model, resolved by the app's data templates).
/// Views are cached by page instance and kept in the tree: switching pages toggles <see cref="Visual.IsVisible"/>
/// instead of detaching. Detaching an Avalonia DataGrid with rows was multi-second; hiding is cheap.
/// </summary>
public class PageHost : Decorator
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

    // Grid stretches children to the host size; plain Panel would leave pages at their desired size.
    private readonly Grid _host = new();
    private readonly Dictionary<object, Control> _views = new();
    private Control? _current;

    public PageHost()
    {
        Child = _host;
    }

    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageProperty)
        {
            ShowPage(change.NewValue);
        }
    }

    private void ShowPage(object? page)
    {
        var perf = TryPerf();

        if (_current is not null)
        {
            var outgoingName = _current.DataContext?.GetType().Name ?? "none";
            var hide = Stopwatch.StartNew();
            _current.IsVisible = false;
            perf?.Record("View", outgoingName, hide.Elapsed, "hide");
            _current = null;
        }

        if (page is null)
        {
            return;
        }

        var view = GetOrCreateView(page);
        if (view.Parent is null)
        {
            _host.Children.Add(view);
        }

        var show = Stopwatch.StartNew();
        view.IsVisible = true;
        _current = view;
        perf?.Record("View", page.GetType().Name, show.Elapsed, "show");
    }

    private Control GetOrCreateView(object page)
    {
        var name = page.GetType().Name;
        var perf = TryPerf();
        if (_views.TryGetValue(page, out var view))
        {
            perf?.Record("View", name, TimeSpan.Zero, "reused");
            return view;
        }

        var sw = Stopwatch.StartNew();
        view = BuildView(page);
        view.IsVisible = false;
        sw.Stop();
        _views[page] = view;
        perf?.Record("View", name, sw.Elapsed, "created");
        return view;
    }

    private Control BuildView(object page)
    {
        var view = this.FindDataTemplate(page)?.Build(page) ?? new TextBlock { Text = page.ToString() };
        view.DataContext = page;
        return view;
    }

    private static PerformanceMonitor? TryPerf()
    {
        try
        {
            return App.Services?.GetService<PerformanceMonitor>() is { IsEnabled: true } perf ? perf : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
