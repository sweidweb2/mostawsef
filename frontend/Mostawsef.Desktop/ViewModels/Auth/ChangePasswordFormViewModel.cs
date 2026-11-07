using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Auth;

/// <summary>The signed-in user changes their own password (opened from the user menu of every portal).</summary>
public partial class ChangePasswordFormViewModel : FormViewModel
{
    private readonly AuthApi _auth;

    public ChangePasswordFormViewModel(AuthApi auth, IDialogService dialogs, LocalizationService localization)
        : base(dialogs, localization)
    {
        _auth = auth;
        MarkClean();
    }

    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    protected override object BuildRequest() => new ChangePasswordRequest { CurrentPassword = CurrentPassword, NewPassword = NewPassword };

    protected override void ValidateMore(FormErrors errors)
    {
        if (ConfirmPassword != NewPassword)
        {
            errors.Set(nameof(ConfirmPassword), ErrorCodes.PasswordMismatch);
        }
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) =>
        await _auth.ChangePasswordAsync((ChangePasswordRequest)request, cancellationToken);
}
