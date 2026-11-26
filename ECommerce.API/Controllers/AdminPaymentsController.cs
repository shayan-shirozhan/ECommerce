using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Payments.ResolveAdminPayment;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/admin/payments")]
[Authorize(Roles = ApplicationRoles.Admin)]
public sealed class AdminPaymentsController(ISender sender) : ControllerBase
{
    [HttpPost("{paymentId:guid}/resolve")]
    [ProducesResponseType(typeof(ResolveAdminPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ResolveAdminPaymentResponse>> ResolvePayment(
        [FromRoute] Guid paymentId,
        [FromBody] ResolveAdminPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await sender.Send(
            new ResolveAdminPaymentCommand(
                paymentId,
                request.Action,
                request.Reason,
                request.RefId),
            cancellationToken);

        return Ok(response);
    }
}

public sealed record ResolveAdminPaymentRequest(
    PaymentResolutionAction Action,
    string Reason,
    long? RefId = null);
