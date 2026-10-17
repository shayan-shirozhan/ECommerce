using ECommerce.API.Contracts.Orders;
using ECommerce.Application.Features.Orders.CreateOrderFromCart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController(ISender sender) : ControllerBase
{
    [HttpPost("from-cart")]
    [ProducesResponseType(typeof(CreateOrderFromCartResponse), StatusCodes.Status200OK)]
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

        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
