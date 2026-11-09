using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal: orders, newest first, with search (patient name, phone or number) and paging; opens New order and Order details.</summary>
public partial class OrdersViewModel(OrdersApi orders, NavigationService navigation, IToastService toasts, LocalizationService localization)
    : NumberedListViewModel<OrderListItemDto>(toasts, localization)
{
    protected override Task<ApiResult<PagedResult<OrderListItemDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        orders.GetOrdersAsync(page, pageSize, search, includeTotal, cancellationToken);

    [RelayCommand]
    private async Task NewOrderAsync()
    {
        if (await navigation.OpenAsync<NewOrderViewModel>())
        {
            MarkStale();
        }
    }

    [RelayCommand]
    private Task ViewAsync(OrderListItemDto? item) =>
        item is null ? Task.CompletedTask : navigation.OpenAsync<OrderDetailsViewModel>(page => page.OrderId = item.Id);
}
