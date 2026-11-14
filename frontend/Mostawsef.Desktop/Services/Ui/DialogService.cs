using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
    /// A danger confirmation for an action that cannot be undone: true only once the user typed <paramref name="word"/>
    /// exactly and confirmed.
    /// </summary>
    Task<bool> ConfirmWithWordAsync(string message, string word, string? title = null, string? confirmText = null);

    /// <summary>
    /// Shows a form in a Modal with Cancel / Save. True once it saved; false when the user cancelled or closed it
    /// (closing with unsaved changes asks first).
    /// </summary>
    Task<bool> ShowFormAsync(FormViewModel form, string title, double maxWidth = 520);
}

public class DialogService : IDialogService
{
    private DialogHost? _host;

    /// <summary>
    /// One idle view per form type: building a form view costs 50-140 ms, re-pointing an existing one at a new
    /// view model only a few. A view is taken out while its dialog is open, so two open forms never share one.
    /// </summary>
    private readonly Dictionary<Type, Control> _idleFormViews = [];

    public DialogService(SessionService session) =>
        session.Changed += (_, _) =>
        {
            if (!session.IsAuthenticated)
            {
                CloseAll();
                _idleFormViews.Clear();
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

    public async Task<bool> ConfirmWithWordAsync(string message, string word, string? title = null, string? confirmText = null) =>
        await ShowAsync(new TypeToConfirmDialog(message, word, title, confirmText)) is true;

    public async Task<bool> ShowFormAsync(FormViewModel form, string title, double maxWidth = 520)
    {
        var formType = form.GetType();
        var view = _idleFormViews.Remove(formType, out var idle) ? idle : AppRoutes.Table.CreateView(formType);

        var modal = new Modal
        {
            Title = title,
            Content = form,
            // Content stays the view model; the template hands over the (possibly reused) view instead of building one.
            ContentTemplate = view is null ? null : new FuncDataTemplate<FormViewModel>((_, _) => view, supportsRecycling: false),
            MaxWidth = maxWidth,
            // Block X/Esc/outside-click while Save is in flight (Cancel button is already CanExecute-gated).
            ConfirmDismiss = async () => !form.IsBusy && await form.ConfirmDiscardAsync(),
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
            if (view is not null)
            {
                // Detach from the closed modal (the view then inherits no DataContext, so it lets go of the old view
                // model) and keep it for the next form of this type. Never set DataContext = null here: a local value
                // would block inheriting the next form's view model.
                modal.Content = null;
                modal.ContentTemplate = null;
                view.ClearValue(StyledElement.DataContextProperty);
                _idleFormViews[formType] = view;
            }
        }
    }

    private static Button FooterButton(string textKey, string style, System.Windows.Input.ICommand command, bool isDefault)
    {
        var button = new Button { Classes = { style }, Command = command, IsDefault = isDefault };
        button.Bind(ContentControl.ContentProperty, TrExtension.CreateBinding(textKey));
        return button;
    }
}
