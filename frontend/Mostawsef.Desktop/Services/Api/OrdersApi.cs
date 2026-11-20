using System;
using System.Collections.Generic;
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

    /// <summary>The Admin dashboard for the days <paramref name="from"/> to <paramref name="to"/> (both included).</summary>
    public Task<ApiResult<OrderDashboardDto>> GetDashboardAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderDashboardDto>($"api/orders/dashboard?{DashboardQuery.Of(from, to)}", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<OrderDetailsDto>($"api/orders/{id}", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> AddPaymentAsync(Guid orderId, AddPaymentRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<AddPaymentRequest, OrderDetailsDto>($"api/orders/{orderId}/payments", request, cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> UpdatePaymentAsync(
        Guid orderId, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdatePaymentRequest, OrderDetailsDto>($"api/orders/{orderId}/payments/{paymentId}", request, cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> DeletePaymentAsync(Guid orderId, Guid paymentId, CancellationToken cancellationToken = default) =>
        api.DeleteAsync<OrderDetailsDto>($"api/orders/{orderId}/payments/{paymentId}", cancellationToken);

    public Task<ApiResult<OrderDetailsDto>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateOrderRequest, OrderDetailsDto>("api/orders", request, cancellationToken);
}
