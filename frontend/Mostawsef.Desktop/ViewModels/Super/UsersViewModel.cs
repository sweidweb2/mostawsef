using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Super portal: users list with search and paging; add, edit, reset password and delete. Access via AppRoutes.</summary>
public partial class UsersViewModel(UsersApi users, IDialogService dialogs, IToastService toasts, LocalizationService localization)
    : NumberedListViewModel<UserListItemDto>(toasts, localization)
{
    protected override Task<ApiResult<PagedResult<UserListItemDto>>> FetchPageAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken) =>
        users.GetUsersAsync(page, pageSize, search, cancellationToken);

    [RelayCommand]
    private async Task AddAsync()
    {
        if (await dialogs.ShowFormAsync(new UserFormViewModel(users, dialogs, Localization), Localization.Get("Users.AddTitle")))
        {
            Toasts.Success(Localization.Get("Users.Saved"));
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(UserListItemDto? item)
    {
        if (item is null || await ReadAsync(ct => users.GetUserAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        if (await dialogs.ShowFormAsync(new UserFormViewModel(users, dialogs, Localization, result.Value), Localization.Get("Users.EditTitle")))
        {
            Toasts.Success(Localization.Get("Users.Saved"));
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserListItemDto? item)
    {
        if (item is not null
            && await dialogs.ShowFormAsync(
                new ResetPasswordFormViewModel(users, item, dialogs, Localization), Localization.Get("Users.ResetPasswordTitle")))
        {
            Toasts.Success(Localization.Get("Users.PasswordReset"));
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(UserListItemDto? item)
    {
        if (item is null
            || !await dialogs.ConfirmAsync(Localization.Get("Users.DeleteConfirm"), confirmText: Localization.Get("Common.Delete"), isDanger: true))
        {
            return;
        }

        var result = await users.DeleteUserAsync(item.Id);
        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        Toasts.Success(Localization.Get("Users.Deleted"));
        await ReloadAsync();
    }
}
