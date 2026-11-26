using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Orders;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Orders;

namespace Mostawsef.Api.Controllers;

/// <summary>Lab test orders. Failures come back as problem responses with an ErrorCodes "code".</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<OrderListItemDto>> List(
        [FromQuery] PageQuery query, [FromQuery] OrderListFilterRequest filter, CancellationToken cancellationToken) =>
        orders.GetPageAsync(query, filter, cancellationToken);

    /// <summary>The Admin dashboard for a period: money (revenue, outstanding) and order counts (days in the server's time zone, kept between the first order and today).</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<OrderDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<OrderDashboardDto> Dashboard([FromQuery] DashboardRangeQuery query, CancellationToken cancellationToken) =>
        orders.GetDashboardAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderDetailsDto> Get(Guid id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderDetailsDto> AddPayment(Guid id, AddPaymentRequest request, CancellationToken cancellationToken) =>
        orders.AddPaymentAsync(id, request, cancellationToken);

    [HttpPut("{id:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderDetailsDto> UpdatePayment(
        Guid id, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken) =>
        orders.UpdatePaymentAsync(id, paymentId, request, cancellationToken);

    [HttpDelete("{id:guid}/payments/{paymentId:guid}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<OrderDetailsDto> DeletePayment(
        Guid id, Guid paymentId, [FromQuery] int? version, CancellationToken cancellationToken) =>
        orders.DeletePaymentAsync(id, paymentId, version, cancellationToken);

    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
}
