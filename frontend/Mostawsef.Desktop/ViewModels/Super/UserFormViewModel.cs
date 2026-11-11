using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Users;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Create a user (no <paramref name="existing"/>) or edit one. The username is fixed once created.</summary>
public partial class UserFormViewModel : FormViewModel
{
    private readonly UsersApi _users;
    private readonly UserListItemDto? _existing;

    public UserFormViewModel(UsersApi users, IDialogService dialogs, LocalizationService localization, UserListItemDto? existing = null)
        : base(dialogs, localization)
    {
        _users = users;
        _existing = existing;

        if (existing is not null)
        {
            FullName = existing.FullName;
            Username = existing.Username;
            Role = existing.Role;
            IsActive = existing.IsActive;
        }

        MarkClean();
    }

    public bool IsNew => _existing is null;

    public IReadOnlyList<string> RoleOptions { get; } = [Roles.Admin, Roles.Super];

    [ObservableProperty]
    public partial string FullName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Role { get; set; } = Roles.Admin;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    protected override object BuildRequest() => _existing is null
        ? new CreateUserRequest { FullName = FullName.Trim(), Username = Username.Trim(), Password = Password, Role = Role }
        : new UpdateUserRequest { FullName = FullName.Trim(), Role = Role, IsActive = IsActive, Version = _existing.Version };

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) => request switch
    {
        CreateUserRequest create => await _users.CreateUserAsync(create, cancellationToken),
        _ => await _users.UpdateUserAsync(_existing!.Id, (UpdateUserRequest)request, cancellationToken),
    };
}
