using ECommerce.API.Contracts.Orders;
using ECommerce.Application.Features.Orders.CreateOrderFromCart;
using ECommerce.Application.Features.Orders.GetMyOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommerce.Application.Features.Orders.GetMyOrderById;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost("from-cart")]
    [ProducesResponseType(
        typeof(CreateOrderFromCartResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateOrderFromCartResponse>> CreateFromCart(
        [FromBody] CreateOrderFromCartRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateOrderFromCartCommand(
            request.ShippingRecipientName,
            request.ShippingPhoneNumber,
            request.ShippingProvince,
            request.ShippingCity,
            request.ShippingPostalCode,
            request.ShippingAddressLine);

        var result = await sender.Send(
            command,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("my")]
    [ProducesResponseType(
        typeof(GetMyOrdersResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GetMyOrdersResponse>> GetMyOrders(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetMyOrdersQuery(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("my/{orderId:guid}")]
    [ProducesResponseType(
        typeof(GetMyOrderByIdResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetMyOrderByIdResponse>> GetMyOrderById(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetMyOrderByIdQuery(orderId), cancellationToken);
        return Ok(result);
    }
}
