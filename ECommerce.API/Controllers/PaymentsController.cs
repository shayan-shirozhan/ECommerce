using ECommerce.API.Options;
using ECommerce.API.Payments;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Features.Payments.CompleteZarinPalPayment;
using ECommerce.Application.Features.Payments.InitiatePayment;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(
    ISender sender,
    IEnumerable<IPaymentGateway> paymentGateways,
    IOptions<FrontendOptions> frontendOptions) : ControllerBase
{
    [HttpPost("orders/{orderId:guid}/initiate")]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InitiatePaymentResponse>> InitiatePayment(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!paymentGateways.Any())
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Payment gateway is not configured." });

        var result = await sender.Send(
            new InitiatePaymentCommand(orderId, PaymentMethod.ZarinPal),
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("zarinpal/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ZarinPalCallback(
        [FromQuery] Guid paymentId,
        [FromQuery(Name = "Authority")] string authority,
        [FromQuery(Name = "Status")] string status,
        CancellationToken cancellationToken)
    {
        if (!paymentGateways.Any())
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Payment gateway is not configured." });

        var result = await sender.Send(
            new CompleteZarinPalPaymentCommand(paymentId, authority, status),
            cancellationToken);

        var redirectUrl = PaymentResultRedirectBuilder.Build(
            frontendOptions.Value.PaymentResultUrl,
            result.OrderId,
            result.PaymentStatus);

        return Redirect(redirectUrl);
    }
}
