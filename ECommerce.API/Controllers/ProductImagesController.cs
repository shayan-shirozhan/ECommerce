using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Catalog.ProductImages.CreateProductImage;
using ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;
using ECommerce.Application.Features.Catalog.ProductImages.GetProductImageById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/product-images")]
[Authorize(Roles = ApplicationRoles.Admin)]
public class ProductImagesController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductImageDto>> GetProductImage(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetProductImageByIdQuery(id), cancellationToken);
        return Ok(image);
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateProductImageCommand command, CancellationToken cancellationToken)
    {
        var imageId = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetProductImage), new { Id = imageId }, new { Id = imageId });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteProductImageCommand(id), cancellationToken);
        return NoContent();
    }
}