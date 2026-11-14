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
    public Task<PagedResult<OrderListItemDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        orders.GetPageAsync(query, cancellationToken);

    /// <summary>Order counts by payment state, for the dashboard.</summary>
    [HttpGet("stats")]
    public Task<OrderStatsDto> Stats(CancellationToken cancellationToken) => orders.GetStatsAsync(cancellationToken);

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
