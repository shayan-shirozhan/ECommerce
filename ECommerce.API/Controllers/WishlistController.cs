using ECommerce.Application.Features.Wishlist.AddWishlistItem;
using ECommerce.Application.Features.Wishlist.GetMyWishlist;
using ECommerce.Application.Features.Wishlist.RemoveWishlistItem;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public sealed class WishlistController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(GetMyWishlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GetMyWishlistResponse>> GetMyWishlist(
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetMyWishlistQuery(),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("items")]
    [ProducesResponseType(typeof(AddWishlistItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddWishlistItemResponse>> AddItem(
        [FromBody] AddWishlistItemCommand command,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(command, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new RemoveWishlistItemCommand(productId),
            cancellationToken);

        return NoContent();
    }
}
