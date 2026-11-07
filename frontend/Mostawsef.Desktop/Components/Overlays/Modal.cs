using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>Dialog surface (title, content, footer). Show it with DialogService.ShowAsync and close it with Close(result).</summary>
public class Modal : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Modal, string?>(nameof(Title));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Modal, object?>(nameof(Footer));

    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<Modal, bool>(nameof(ShowCloseButton), true);

    public static readonly DirectProperty<Modal, ICommand> CloseCommandProperty =
        AvaloniaProperty.RegisterDirect<Modal, ICommand>(nameof(CloseCommand), o => o.CloseCommand);

    public Modal()
    {
        CloseCommand = new RelayCommand<object?>(Close);
    }

    public event EventHandler<object?>? CloseRequested;

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>Closes with the given result. Bind buttons with CommandParameter to return values.</summary>
    public ICommand CloseCommand { get; }

    public void Close(object? result = null) => CloseRequested?.Invoke(this, result);
}
