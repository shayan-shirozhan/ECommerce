using ECommerce.API.Contracts.Catalog;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Catalog.Categories.CreateCategory;
using ECommerce.Application.Features.Catalog.Categories.DeleteCategory;
using ECommerce.Application.Features.Catalog.Categories.GetCategoryById;
using ECommerce.Application.Features.Catalog.Categories.UpdateCategory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = ApplicationRoles.Admin)]
public class CategoriesController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await sender.Send(
            new GetCategoryByIdQuery(id),
            cancellationToken);

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult> Create(
        CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var categoryId = await sender.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = categoryId },
            new { id = categoryId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateCategoryCommand(
                id,
                request.Name,
                request.ParentCategoryId),
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeleteCategoryCommand(id),
            cancellationToken);

        return NoContent();
    }
}
