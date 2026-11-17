using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Options;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace ECommerce.Application.Features.Payments.CompleteMockPayment;

public sealed class CompleteMockPaymentCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    IMockPaymentGateway paymentGateway,
    IOptions<OrderExpirationOptions> expirationOptions,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CompleteMockPaymentCommand, CompleteMockPaymentResponse>
{
    public async Task<CompleteMockPaymentResponse> Handle(
        CompleteMockPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByPaymentIdForUserForUpdateAsync(
            command.PaymentId,
            userId,
            cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(
                $"Payment with id '{command.PaymentId}' was not found.");
        }

        var payment = order.Payments
            .FirstOrDefault(candidate => candidate.Id == command.PaymentId);

        if (payment is null)
        {
            throw new NotFoundException(
                $"Payment with id '{command.PaymentId}' was not found.");
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new BusinessRuleException(
                "Only pending payments can be completed.");
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            throw new BusinessRuleException(
                "Only orders awaiting payment can be completed.");
        }

        var paymentWindow = TimeSpan.FromMinutes(
            expirationOptions.Value.PaymentWindowMinutes);

        var cutoffUtc = DateTime.UtcNow.Subtract(paymentWindow);

        if (order.CreatedAt <= cutoffUtc)
        {
            throw new BusinessRuleException(
                "The payment window for this order has expired.");
        }

        var gatewayResult = await paymentGateway.ProcessAsync(
            payment.Id,
            payment.Amount,
            cancellationToken);

        if (gatewayResult.IsSuccessful)
        {
            if (string.IsNullOrWhiteSpace(gatewayResult.TransactionId))
            {
                throw new InvalidOperationException(
                    "Successful payment result must contain a transaction ID.");
            }

            payment.MarkSuccessful(
                gatewayResult.TransactionId,
                gatewayResult.GatewayReference);

            order.Confirm();
        }
        else
        {
            // A failed attempt does not cancel the order. The customer can retry
            // with a new Payment while the order remains AwaitingPayment.
            payment.MarkFailed();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CompleteMockPaymentResponse(
            payment.Id,
            order.Id,
            payment.Status,
            order.Status,
            payment.TransactionId,
            payment.GatewayReference);
    }
}
