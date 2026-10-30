using ECommerce.Application.Features.Payments.CompleteMockPayment;
using ECommerce.Application.Features.Payments.InitiatePayment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(ISender sender) : ControllerBase
{
    [HttpPost("orders/{orderId:guid}/initiate")]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InitiatePaymentResponse>> InitiatePayment(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InitiatePaymentCommand(orderId),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{paymentId:guid}/complete-mock")]
    [ProducesResponseType(typeof(CompleteMockPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompleteMockPaymentResponse>> CompleteMockPayment(
        [FromRoute] Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CompleteMockPaymentCommand(paymentId),
            cancellationToken);

        return Ok(result);
    }
}
