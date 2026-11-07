using System;
using System.Threading.Tasks;
using Mostawsef.Desktop.Components;

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
}

public class DialogService : IDialogService
{
    private DialogHost? _host;

    public void Attach(DialogHost host) => _host = host;

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
}
