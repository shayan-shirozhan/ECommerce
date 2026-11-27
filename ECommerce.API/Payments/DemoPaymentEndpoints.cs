using ECommerce.Application.Features.Payments.CompleteMockPayment;
using ECommerce.Application.Features.Payments.InitiatePayment;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.API.Payments;

/// <summary>
/// HTTP endpoints for the isolated public Demo payment flow.
/// The application maps these endpoints only when DemoPaymentEnvironment is enabled.
/// </summary>
internal static class DemoPaymentEndpoints
{
    public static IEndpointRouteBuilder MapDemoPaymentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/demo/payments/orders/{orderId:guid}/initiate",
                async (
                    Guid orderId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new InitiatePaymentCommand(orderId, PaymentMethod.Mock),
                        cancellationToken);

                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithTags("Demo Payments")
            .WithName("InitiateDemoPayment")
            .Produces<InitiatePaymentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        endpoints.MapPost(
                "/api/demo/payments/{paymentId:guid}/complete",
                async (
                    Guid paymentId,
                    ISender sender,
                    CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new CompleteMockPaymentCommand(paymentId),
                        cancellationToken);

                    return Results.Ok(result);
                })
            .RequireAuthorization()
            .WithTags("Demo Payments")
            .WithName("CompleteDemoPayment")
            .Produces<CompleteMockPaymentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
