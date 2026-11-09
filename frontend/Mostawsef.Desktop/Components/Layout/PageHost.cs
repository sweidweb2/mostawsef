using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Shows the view of <see cref="Page"/> (a view model, resolved by the app's data templates).
/// A new view is built each time the page changes so off-screen menu pages are not kept alive
/// (memory and language-switch cost stay with the visible page only).
/// </summary>
public class PageHost : Decorator
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

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
            Child = change.NewValue is { } page ? BuildView(page) : null;
        }
    }

    private Control BuildView(object page)
    {
        var view = this.FindDataTemplate(page)?.Build(page) ?? new TextBlock { Text = page.ToString() };
        view.DataContext = page;
        return view;
    }
}
