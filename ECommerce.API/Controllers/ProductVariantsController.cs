using ECommerce.API.Contracts.Catalog;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Catalog.ProductVariants.CreateProductVariant;
using ECommerce.Application.Features.Catalog.ProductVariants.ActivateProductVariant;
using ECommerce.Application.Features.Catalog.ProductVariants.DeactivateProductVariant;
using ECommerce.Application.Features.Catalog.ProductVariants.GetProductVariantById;
using ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/product-variants")]
[Authorize(Roles = ApplicationRoles.Admin)]
public class ProductVariantsController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductVariantDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var variant = await sender.Send(new GetProductVariantByIdQuery(id), cancellationToken);
        return Ok(variant);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductVariantRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateProductVariantCommand(id, request.SKU, request.Name),
            cancellationToken);

        return NoContent();
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new ActivateProductVariantCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateProductVariantCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateProductVariantsCommand command, CancellationToken cancellationToken)
    {
        var variantId = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = variantId }, new { id = variantId });
    }
}
