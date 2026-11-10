using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Like a ContentControl, but keeps each content's view mounted and toggles <see cref="Visual.IsVisible"/>.
/// Use for heavy step/wizard UIs where swapping <c>Content</c> would rebuild DataGrids or large lists.
/// Each content value gets its own inner ContentControl so Avalonia DataTemplates resolve normally.
/// </summary>
public class CachedContentHost : Decorator
{
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<CachedContentHost, object?>(nameof(Content));

    private readonly Grid _host = new();
    private readonly Dictionary<object, ContentControl> _views = new();
    private ContentControl? _current;

    public CachedContentHost()
    {
        Child = _host;
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
        {
            Show(change.NewValue);
        }
    }

    private void Show(object? content)
    {
        if (_current is not null)
        {
            _current.IsVisible = false;
            _current = null;
        }

        if (content is null)
        {
            return;
        }

        if (!_views.TryGetValue(content, out var view))
        {
            view = new ContentControl
            {
                Content = content,
                IsVisible = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };
            _views[content] = view;
            _host.Children.Add(view);
        }

        view.IsVisible = true;
        _current = view;
    }
}
