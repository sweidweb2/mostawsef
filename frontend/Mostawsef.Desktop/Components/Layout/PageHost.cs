using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Shows the view of <see cref="Page"/> (a view model, resolved by the app's data templates).
/// Views are cached by page instance and kept in the tree: switching pages toggles <see cref="Visual.IsVisible"/>
/// instead of detaching. Detaching an Avalonia DataGrid with rows was multi-second; hiding is cheap.
/// Hidden cached views are dropped on a language switch so every mounted page is not rebound.
/// Transient sub-pages must call <see cref="Release"/> when discarded so their views do not accumulate.
/// </summary>
public class PageHost : Decorator
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

    /// <summary>Optional; set from app startup / tests so this control does not resolve DI itself.</summary>
    public static PerformanceMonitor? Perf { get; set; }

    /// <summary>Optional; set from app startup / tests so language switches can drop hidden views.</summary>
    public static LocalizationService? Localization { get; set; }

    // Grid stretches children to the host size; plain Panel would leave pages at their desired size.
    private readonly Grid _host = new();
    private readonly Dictionary<object, Control> _views = new();
    private Control? _current;
    private LocalizationService? _subscribedLocalization;

    public PageHost()
    {
        Child = _host;
    }

    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    /// <summary>Drops a discarded page's view from the cache and the visual tree.</summary>
    public void Release(object page)
    {
        if (!_views.Remove(page, out var view))
        {
            return;
        }

        if (ReferenceEquals(_current, view))
        {
            _current = null;
        }

        _host.Children.Remove(view);
        view.DataContext = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeLanguage();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeLanguage();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageProperty)
        {
            ShowPage(change.NewValue);
        }
    }

    private void SubscribeLanguage()
    {
        if (Localization is not { } localization || ReferenceEquals(_subscribedLocalization, localization))
        {
            return;
        }

        UnsubscribeLanguage();
        _subscribedLocalization = localization;
        localization.LanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLanguage()
    {
        if (_subscribedLocalization is null)
        {
            return;
        }

        _subscribedLocalization.LanguageChanged -= OnLanguageChanged;
        _subscribedLocalization = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => DiscardHiddenViews();

    /// <summary>Keeps the visible page; drops the rest so a language switch does not rebind every visited view.</summary>
    private void DiscardHiddenViews()
    {
        foreach (var (page, view) in _views.ToList())
        {
            if (ReferenceEquals(view, _current))
            {
                continue;
            }

            _views.Remove(page);
            _host.Children.Remove(view);
            view.DataContext = null;
        }
    }

    private void ShowPage(object? page)
    {
        var perf = Perf is { IsEnabled: true } enabled ? enabled : null;

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

        var view = GetOrCreateView(page, perf);
        if (view.Parent is null)
        {
            _host.Children.Add(view);
        }

        var show = Stopwatch.StartNew();
        view.IsVisible = true;
        _current = view;
        perf?.Record("View", page.GetType().Name, show.Elapsed, "show");
    }

    private Control GetOrCreateView(object page, PerformanceMonitor? perf)
    {
        var name = page.GetType().Name;
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
}
