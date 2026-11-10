using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Sellers.SellerRequests.ApproveSellerRequest;
using ECommerce.Application.Features.Sellers.SellerRequests.CreateSellerRequest;
using ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequestById;
using ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;
using ECommerce.Application.Features.Sellers.SellerRequests.RejectSellerRequest;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/seller-requests")]
public class SellerRequestsController(ISender sender) : ControllerBase
{
    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpGet]
    [ProducesResponseType(
        typeof(GetSellerRequestsResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetSellerRequestsResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] SellerRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var response = await sender.Send(
            new GetSellerRequestsQuery(page, pageSize, status),
            cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SellerRequestDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var sellerRequest = await sender.Send(new GetSellerRequestByIdQuery(id), cancellationToken);
        return Ok(sellerRequest);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult> Create(CreateSellerRequestCommand command, CancellationToken cancellationToken)
    {
        var sellerRequestId = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = sellerRequestId }, new { id = sellerRequestId });
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ApproveSellerRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = ApplicationRoles.Admin)]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RejectSellerRequestCommand(id), cancellationToken);
        return NoContent();
    }
}
