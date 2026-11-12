using ECommerce.Application.Features.Addresses.CreateAddress;
using ECommerce.Application.Features.Addresses.GetMyAddresses;
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
