using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Services.Ui;

public interface IDialogService
{
    /// <summary>Shows a Modal and completes with the value passed to Modal.Close(result).</summary>
    Task<object?> ShowAsync(Modal modal);

    Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null,
        bool isDanger = false);

    /// <summary>
    /// Shows a form in a Modal with Cancel / Save. True once it saved; false when the user cancelled or closed it
    /// (closing with unsaved changes asks first).
    /// </summary>
    Task<bool> ShowFormAsync(FormViewModel form, string title, double maxWidth = 520);
}

public class DialogService : IDialogService
{
    private DialogHost? _host;

    public DialogService(SessionService session) =>
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                CloseAll();
            }
        };

    public void Attach(DialogHost host) => _host = host;

    /// <summary>Force-closes every open dialog (no discard prompt). Used on sign-out.</summary>
    public void CloseAll() => _host?.CloseAll();

    public Task<object?> ShowAsync(Modal modal)
    {
        if (_host is null)
        {
            throw new InvalidOperationException("DialogService has no DialogHost attached.");
        }

        var completion = new TaskCompletionSource<object?>();

        void OnCloseRequested(object? sender, object? result)
        {
            modal.CloseRequested -= OnCloseRequested;
            _host.Hide(modal);
            completion.TrySetResult(result);
        }

        modal.CloseRequested += OnCloseRequested;
        _host.Show(modal);
        return completion.Task;
    }

    public async Task<bool> ConfirmAsync(
        string message,
        string? title = null,
        string? confirmText = null,
        string? cancelText = null,
        bool isDanger = false)
    {
        var result = await ShowAsync(new ConfirmDialog(message, title, confirmText, cancelText, isDanger));
        return result is true;
    }

    public async Task<bool> ShowFormAsync(FormViewModel form, string title, double maxWidth = 520)
    {
        var modal = new Modal
        {
            Title = title,
            Content = form,
            MaxWidth = maxWidth,
            ConfirmDismiss = form.ConfirmDiscardAsync,
            Footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children =
                {
                    FooterButton("Common.Cancel", "secondary", form.CancelCommand, isDefault: false),
                    FooterButton("Common.Save", "primary", form.SaveCommand, isDefault: true),
                },
            },
        };

        void OnClosed(object? sender, bool saved) => modal.Close(saved);

        form.Closed += OnClosed;
        try
        {
            return await ShowAsync(modal) is true;
        }
        finally
        {
            form.Closed -= OnClosed;
            form.CancelLifetime();
        }
    }

    private static Button FooterButton(string textKey, string style, System.Windows.Input.ICommand command, bool isDefault)
    {
        var button = new Button { Classes = { style }, Command = command, IsDefault = isDefault };
        button.Bind(ContentControl.ContentProperty, TrExtension.CreateBinding(textKey));
        return button;
    }
}
