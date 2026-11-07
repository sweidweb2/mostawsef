using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Base of every create/edit form. Show it with IDialogService.ShowFormAsync.
/// <list type="bullet">
/// <item>Input properties are named like the request properties, so the request's validation attributes
/// (checked here before sending) and the API's field errors land on the same <see cref="Errors"/> entry.</item>
/// <item>Save: validates, sends, maps a failure to field errors or <see cref="FormErrorKey"/>, closes on success.</item>
/// <item>IsDirty compares the current request with the one at load/save time (requests are records, so equality is by value);
/// Cancel and closing the dialog ask before discarding changes.</item>
/// </list>
/// Subclasses fill their fields, then call <see cref="MarkClean"/> at the end of the constructor.
/// </summary>
public abstract partial class FormViewModel : ViewModelBase
{
    private static readonly HashSet<string> StateProperties =
        [nameof(IsBusy), nameof(IsDirty), nameof(FormErrorKey), nameof(HasFormError)];

    private object? _savedRequest;

    protected FormViewModel(IDialogService dialogs, LocalizationService localization)
    {
        Dialogs = dialogs;
        Localization = localization;
    }

    /// <summary>Raised with true after a successful save, false when the user cancels.</summary>
    public event EventHandler<bool>? Closed;

    protected IDialogService Dialogs { get; }

    protected LocalizationService Localization { get; }

    public FormErrors Errors { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Resource key of an error that belongs to no field (e.g. a conflict or the server being down).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFormError))]
    public partial string? FormErrorKey { get; private set; }

    public bool HasFormError => FormErrorKey is not null;

    /// <summary>The request to send, built from the current field values (trim text here).</summary>
    protected abstract object BuildRequest();

    protected abstract Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken);

    /// <summary>Rules the request attributes cannot express, e.g. a matching "confirm password".</summary>
    protected virtual void ValidateMore(FormErrors errors)
    {
    }

    /// <summary>The current values become the "unchanged" state.</summary>
    protected void MarkClean()
    {
        _savedRequest = BuildRequest();
        IsDirty = false;
    }

    /// <summary>True when there is nothing to lose, or the user agreed to discard the changes.</summary>
    public async Task<bool> ConfirmDiscardAsync() =>
        !IsDirty || await Dialogs.ConfirmAsync(
            Localization.Get("Form.DiscardMessage"),
            Localization.Get("Form.DiscardTitle"),
            Localization.Get("Form.Discard"),
            Localization.Get("Form.KeepEditing"),
            isDanger: true);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_savedRequest is null || e.PropertyName is null || StateProperties.Contains(e.PropertyName))
        {
            return;
        }

        Errors.Clear(e.PropertyName);
        IsDirty = !Equals(BuildRequest(), _savedRequest);
    }

    private bool CanSave() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        FormErrorKey = null;
        Errors.Clear();

        var request = BuildRequest();
        if (!Validate(request))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await SubmitAsync(request, CancellationToken.None);
            if (result.IsSuccess)
            {
                _savedRequest = request;
                IsDirty = false;
                Closed?.Invoke(this, true);
                return;
            }

            ShowFailure(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (await ConfirmDiscardAsync())
        {
            Closed?.Invoke(this, false);
        }
    }

    private bool Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var field in result.MemberNames)
            {
                if (Errors[field] is null)
                {
                    Errors.Set(field, result.ErrorMessage ?? ErrorCodes.Invalid);
                }
            }
        }

        ValidateMore(Errors);
        return !Errors.HasErrors;
    }

    /// <summary>Field errors go next to their field; anything else becomes the form-level message.</summary>
    private void ShowFailure(IApiResult result)
    {
        var problem = result.Problem ?? ApiProblem.Empty;
        foreach (var (field, codes) in problem.FieldErrors)
        {
            if (codes.Length == 0)
            {
                continue;
            }

            if (IsField(field))
            {
                Errors.Set(field, codes[0]);
            }
            else
            {
                FormErrorKey ??= TranslatableCode(codes[0]) ?? "Error.RequestFailed";
            }
        }

        if (Errors.HasErrors || FormErrorKey is not null)
        {
            return;
        }

        FormErrorKey = result.Error == ApiError.Unreachable
            ? "Login.ServerUnreachable"
            : TranslatableCode(problem.Code) ?? "Error.RequestFailed";
    }

    private bool IsField(string name) =>
        GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is not null;

    /// <summary>"ErrorCode.&lt;code&gt;" when that text exists (arguments after '|' are not used at form level).</summary>
    private string? TranslatableCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        var key = "ErrorCode." + code.Split(ErrorCodes.ArgumentSeparator)[0];
        return Localization.Get(key) == key ? null : key;
    }
}
