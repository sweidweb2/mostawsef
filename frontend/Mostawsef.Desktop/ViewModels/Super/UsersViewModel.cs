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
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        users.GetUsersAsync(page, pageSize, search, includeTotal, cancellationToken);

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
        // List rows already include Version; no GET needed (same pattern as lab tests).
        if (item is null)
        {
            return;
        }

        if (await dialogs.ShowFormAsync(new UserFormViewModel(users, dialogs, Localization, item), Localization.Get("Users.EditTitle")))
        {
            Toasts.Success(Localization.Get("Users.Saved"));
            await ReloadAsync(includeTotal: false);
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

        if (await ReadAsync(ct => users.DeleteUserAsync(item.Id, ct)) is not { } result)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ShowError(result);
            return;
        }

        Toasts.Success(Localization.Get("Users.Deleted"));
        await ReloadAsync();
    }
}
