using ECommerce.Application.Features.Addresses.CreateAddress;
using ECommerce.Application.Features.Addresses.DeleteAddress;
using ECommerce.Application.Features.Addresses.GetMyAddresses;
using ECommerce.Application.Features.Addresses.SetDefaultAddress;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public sealed class AddressesController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateAddressResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateAddressResponse>> Create(
        [FromBody] CreateAddressCommand command,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetMyAddresses), new { }, response);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ECommerce.API.Contracts.Addresses.UpdateAddressRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ECommerce.Application.Features.Addresses.UpdateAddress.UpdateAddressCommand(
                id,
                request.Title,
                request.RecipientName,
                request.PhoneNumber,
                request.Province,
                request.City,
                request.PostalCode,
                request.AddressLine),
            cancellationToken);

        return NoContent();
    }


    [HttpPut("{id:guid}/default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetDefault(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new SetDefaultAddressCommand(id),
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeleteAddressCommand(id),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(GetMyAddressesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GetMyAddressesResponse>> GetMyAddresses(
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new GetMyAddressesQuery(),
            cancellationToken);

        return Ok(response);
    }
}
