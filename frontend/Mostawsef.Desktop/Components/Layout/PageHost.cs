using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Shows the view of <see cref="Page"/> (a view model, resolved by the app's data templates).
/// Views are cached by page instance so returning to a reused menu view model reattaches the same
/// view instead of rebuilding it. Sub-pages that create a new view model each time still get a new view.
/// </summary>
public class PageHost : Decorator
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

    private readonly Dictionary<object, Control> _views = new();

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
            Child = change.NewValue is { } page ? GetOrCreateView(page) : null;
        }
    }

    private Control GetOrCreateView(object page)
    {
        if (_views.TryGetValue(page, out var view))
        {
            return view;
        }

        view = BuildView(page);
        _views[page] = view;
        return view;
    }

    private Control BuildView(object page)
    {
        var view = this.FindDataTemplate(page)?.Build(page) ?? new TextBlock { Text = page.ToString() };
        view.DataContext = page;
        return view;
    }
}
