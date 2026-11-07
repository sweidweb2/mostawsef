using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>A Super sets a new password for another user.</summary>
public partial class ResetPasswordFormViewModel : FormViewModel
{
    private readonly UsersApi _users;

    public ResetPasswordFormViewModel(UsersApi users, UserListItemDto user, IDialogService dialogs, LocalizationService localization)
        : base(dialogs, localization)
    {
        _users = users;
        User = user;
        MarkClean();
    }

    public UserListItemDto User { get; }

    [ObservableProperty]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    protected override object BuildRequest() => new ResetPasswordRequest { NewPassword = NewPassword };

    protected override void ValidateMore(FormErrors errors)
    {
        if (ConfirmPassword != NewPassword)
        {
            errors.Set(nameof(ConfirmPassword), ErrorCodes.PasswordMismatch);
        }
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) =>
        await _users.ResetPasswordAsync(User.Id, (ResetPasswordRequest)request, cancellationToken);
}
