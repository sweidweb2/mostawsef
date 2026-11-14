using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Orders;
using Mostawsef.Contracts.Common;
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

    /// <summary>Order counts by payment state, for the dashboard.</summary>
    [HttpGet("stats")]
    public Task<OrderStatsDto> Stats(CancellationToken cancellationToken) => orders.GetStatsAsync(cancellationToken);

    /// <summary>Orders created per day (default the last 30 days), for the dashboard line graph.</summary>
    [HttpGet("daily")]
    public Task<IReadOnlyList<OrderDailyCountDto>> Daily([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        orders.GetDailyCountsAsync(days, cancellationToken);

    /// <summary>The most ordered tests (default 20), for the dashboard line graph.</summary>
    [HttpGet("top-tests")]
    public Task<IReadOnlyList<TopTestDto>> TopTests([FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        orders.GetTopTestsAsync(take, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderDetailsDto> Get(Guid id, CancellationToken cancellationToken) => orders.GetAsync(id, cancellationToken);

    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<OrderDetailsDto> AddPayment(Guid id, AddPaymentRequest request, CancellationToken cancellationToken) =>
        orders.AddPaymentAsync(id, request, cancellationToken);

    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
}
