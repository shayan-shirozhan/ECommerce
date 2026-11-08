using ECommerce.API.Contracts.Orders;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Orders.GetSellerOrderById;
using ECommerce.Application.Features.Orders.GetSellerOrders;
using ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;
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

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(
        typeof(GetSellerOrderByIdResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetSellerOrderByIdResponse>> GetSellerOrderById(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetSellerOrderByIdQuery(orderId),
            cancellationToken);

        return Ok(response);
    }

    [HttpPatch("{orderId:guid}/status")]
    [ProducesResponseType(
        typeof(UpdateSellerOrderStatusResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateSellerOrderStatusResponse>> UpdateSellerOrderStatus(
        Guid orderId,
        [FromBody] UpdateSellerOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new UpdateSellerOrderStatusCommand(
                orderId,
                request.NewStatus),
            cancellationToken);

        return Ok(response);
    }
}
