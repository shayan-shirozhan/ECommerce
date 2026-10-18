using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Catalog.Products.ActivateProduct;
using ECommerce.Application.Features.Catalog.Products.CreateProduct;
using ECommerce.Application.Features.Catalog.Products.DeactivateProduct;
using ECommerce.Application.Features.Catalog.Products.GetProductById;
using ECommerce.Application.Features.Catalog.Products.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Roles = ApplicationRoles.Admin)]
public class ProductsController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<GetProductsResponse>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetProductsQuery(page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDetailsDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetProductByIdQuery(id),
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ActivateProductCommand(id),
            cancellationToken);

        return NoContent();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeactivateProductCommand(id),
            cancellationToken);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var productId = await sender.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = productId },
            new { id = productId });
    }
}
