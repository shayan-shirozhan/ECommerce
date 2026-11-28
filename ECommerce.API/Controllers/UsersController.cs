using ECommerce.API.Contracts.Users;
using ECommerce.API.RateLimiting;
using ECommerce.Application.Features.Users.ChangeMyPassword;
using ECommerce.Application.Features.Users.GetMyProfile;
using ECommerce.Application.Features.Users.UpdateMyProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(GetMyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetMyProfileResponse>> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetMyProfileQuery(), cancellationToken);
        return Ok(response);
    }

    [HttpPut("me")]
    [WriteRateLimited]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateMyProfileCommand(request.FirstName, request.LastName),
            cancellationToken);

        return NoContent();
    }

    [HttpPut("me/password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangeMyPassword(
        [FromBody] ChangeMyPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return NoContent();
    }
}
