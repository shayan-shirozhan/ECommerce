using ECommerce.Application.Features.Cart.AddCartItem;
using ECommerce.Application.Features.Cart.GetCart;
using ECommerce.Application.Features.Cart.UpdateCartItem;
using ECommerce.API.Contracts.Cart;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public sealed class CartController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(GetCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetCartResponse>> GetCart(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCartQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPut("items/{sellerOfferId:guid}")]
    [ProducesResponseType(typeof(UpdateCartItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateCartItemResponse>> UpdateItem(
        [FromRoute] Guid sellerOfferId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateCartItemCommand(sellerOfferId, request.Quantity);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(AddCartItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddCartItemResponse>> AddItem(
        [FromBody] AddCartItemCommand command,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
