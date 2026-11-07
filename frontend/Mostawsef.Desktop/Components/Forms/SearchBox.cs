using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

/// <summary>TextBox with a search icon and a clear button. Enter executes SearchCommand with the text.</summary>
public class SearchBox : TextBox
{
    public static readonly StyledProperty<ICommand?> SearchCommandProperty =
        AvaloniaProperty.Register<SearchBox, ICommand?>(nameof(SearchCommand));

    private readonly Button _clearButton;

    public SearchBox()
    {
        Bind(PlaceholderTextProperty, TrExtension.CreateBinding("Common.Search"));

        InnerLeftContent = new PathIcon
        {
            Data = Icons.Get("IconSearch"),
            Width = 16,
            Height = 16,
            Margin = new Thickness(10, 0, 0, 0),
            Opacity = 0.6,
        };

        _clearButton = new Button
        {
            Classes = { "ghost", "icon", "sm" },
            Content = new PathIcon { Data = Icons.Get("IconClose") },
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Focusable = false,
        };
        _clearButton.Click += (_, _) => Clear();
        InnerRightContent = _clearButton;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    public ICommand? SearchCommand
    {
        get => GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            _clearButton.IsVisible = !string.IsNullOrEmpty(Text);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SearchCommand?.CanExecute(Text) == true)
        {
            SearchCommand.Execute(Text);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
        {
            Clear();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
