using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Orders.GetAdminOrderById;
using ECommerce.Application.Features.Orders.GetAdminOrders;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class AdminOrdersController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(GetAdminOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetAdminOrdersResponse>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] DateTimeOffset? createdFromUtc = null,
        [FromQuery] DateTimeOffset? createdToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var response = await sender.Send(
            new GetAdminOrdersQuery(
                page,
                pageSize,
                status,
                createdFromUtc,
                createdToUtc),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(GetAdminOrderByIdResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetAdminOrderByIdResponse>> GetOrderById(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetAdminOrderByIdQuery(orderId),
            cancellationToken);

        return Ok(response);
    }
}
