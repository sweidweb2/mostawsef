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
        int page, int pageSize, string? search, bool includeTotal = true, string? status = null, Guid? patientId = null, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<OrderListItemDto>>(
            $"api/orders?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}") +
            (string.IsNullOrEmpty(status) ? string.Empty : $"&status={Uri.EscapeDataString(status)}") +
            (patientId is { } patient ? $"&patientId={patient}" : string.Empty),
            cancellationToken);

    public Task<ApiResult<OrderStatsDto>> GetStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderStatsDto>("api/orders/stats", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderDetailsDto>($"api/orders/{id}", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> AddPaymentAsync(Guid orderId, AddPaymentRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<AddPaymentRequest, OrderDetailsDto>($"api/orders/{orderId}/payments", request, cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateOrderRequest, OrderDetailsDto>("api/orders", request, cancellationToken);
}
