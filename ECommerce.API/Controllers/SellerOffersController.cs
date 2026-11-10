using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.ActiveSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.CreateSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.DeactivateSellerOffer;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.UpdateSellerOfferPrice;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.UpdateSellerOfferStock;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/seller-offers")]
[Authorize(Roles = ApplicationRoles.Seller)]
public class SellerOffersController(ISender sender) : ControllerBase
{

    [HttpGet("my")]
    [ProducesResponseType(
        typeof(GetMySellerOffersResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetMySellerOffersResponse>> GetMySellerOffers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] SellerOfferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var response = await sender.Send(
            new GetMySellerOffersQuery(page, pageSize, status),
            cancellationToken);

        return Ok(response);
    }


    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SellerOfferDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var offer = await sender.Send(new GetSellerOfferByIdQuery(id), cancellationToken);
        return Ok(offer);
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateSellerOfferCommand command, CancellationToken cancellationToken)
    {
        var offerId = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = offerId }, new { id = offerId });
    }

    [HttpPut("price")]
    public async Task<IActionResult> UpdatePrice(UpdateSellerOfferPriceCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPut("stock")]
    public async Task<IActionResult> UpdateStock(UpdateSellerOfferStockCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPut("activate")]
    public async Task<IActionResult> Activate(ActivateSellerOfferCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPut("deactivate")]
    public async Task<IActionResult> Deactivate(DeactivateSellerOfferCommand command,
        CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }
}
