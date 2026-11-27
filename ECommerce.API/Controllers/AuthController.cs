using ECommerce.API.Contracts.Authentication;
using ECommerce.API.RateLimiting;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Authentication.ForgotPassword;
using ECommerce.Application.Features.Authentication.Login;
using ECommerce.Application.Features.Authentication.Logout;
using ECommerce.Application.Features.Authentication.ResetPassword;
using ECommerce.Application.Features.Authentication.Refresh;
using ECommerce.Application.Features.Authentication.Register;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(ISender sender, IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshTokenCookieName = "ecommerce.refresh-token";
    private const string RefreshTokenCookiePath = "/api/auth";

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new
        {
            result.AccessToken,
            result.ExpiresAtUtc
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException("A refresh token cookie is required.");
        }

        try
        {
            var result = await sender.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
            SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAtUtc);

            return Ok(new
            {
                result.AccessToken,
                result.ExpiresAtUtc
            });
        }
        catch (UnauthorizedException)
        {
            DeleteRefreshTokenCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            await sender.Send(new LogoutCommand(refreshToken), cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return Accepted(response);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new ResetPasswordCommand(request.Email, request.Token, request.NewPassword),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("register")]
    [WriteRateLimited]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = RefreshTokenCookiePath,
                Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
                IsEssential = true
            });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !environment.IsDevelopment() || Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = RefreshTokenCookiePath
            });
    }
}
