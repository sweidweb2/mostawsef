using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Dialog surface (title, content, footer). Show it with DialogService.ShowAsync and close it with Close(result).
/// The X button and Esc dismiss it (result null), asking <see cref="ConfirmDismiss"/> first when set.
/// </summary>
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

    public static readonly DirectProperty<Modal, ICommand> DismissCommandProperty =
        AvaloniaProperty.RegisterDirect<Modal, ICommand>(nameof(DismissCommand), o => o.DismissCommand);

    public Modal()
    {
        CloseCommand = new RelayCommand<object?>(Close);
        DismissCommand = new AsyncRelayCommand(DismissAsync);
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

    /// <summary>Asked before the X button or Esc closes the dialog; false keeps it open (e.g. unsaved changes).</summary>
    public Func<Task<bool>>? ConfirmDismiss { get; set; }

    /// <summary>Closes with the given result. Bind buttons with CommandParameter to return values.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>The X button: closes with null after <see cref="ConfirmDismiss"/> agrees.</summary>
    public ICommand DismissCommand { get; }

    public void Close(object? result = null) => CloseRequested?.Invoke(this, result);

    public async Task DismissAsync()
    {
        if (ConfirmDismiss is null || await ConfirmDismiss())
        {
            Close();
        }
    }
}
