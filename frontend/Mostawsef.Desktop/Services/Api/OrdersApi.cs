using System;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Lab test orders (Admin only).</summary>
public class OrdersApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<OrderListItemDto>>> GetOrdersAsync(
        int page, int pageSize, string? search, bool includeTotal = true, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<OrderListItemDto>>(
            $"api/orders?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}"),
            cancellationToken);

    public Task<ApiResult<OrderStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderStatsDto>("api/orders/stats", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderDetailsDto>($"api/orders/{id}", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateOrderRequest, OrderDetailsDto>("api/orders", request, cancellationToken);
}
