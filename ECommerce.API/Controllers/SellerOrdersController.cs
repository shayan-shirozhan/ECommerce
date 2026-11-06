using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Orders.GetSellerOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/seller-orders")]
[Authorize(Roles = ApplicationRoles.Seller)]
public sealed class SellerOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(GetSellerOrdersResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetSellerOrdersResponse>> GetSellerOrders(
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetSellerOrdersQuery(),
            cancellationToken);

        return Ok(response);
    }
}
