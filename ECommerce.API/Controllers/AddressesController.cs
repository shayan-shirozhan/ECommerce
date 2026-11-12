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
