using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Options;
using ECommerce.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace ECommerce.Application.Features.Payments.InitiatePayment;

public sealed class InitiatePaymentCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    IEnumerable<IPaymentGateway> paymentGateways,
    IOptions<OrderExpirationOptions> expirationOptions,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    public async Task<InitiatePaymentResponse> Handle(
        InitiatePaymentCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForUserForUpdateAsync(
            command.OrderId,
            userId,
            cancellationToken);

        if (order is null)
            throw new NotFoundException($"Order with id '{command.OrderId}' was not found.");

        var paymentGateway = command.PaymentMethod switch
        {
            PaymentMethod.Mock => null,
            PaymentMethod.ZarinPal => paymentGateways.SingleOrDefault()
                ?? throw new PaymentGatewayException(
                    "ZarinPal payment gateway is not configured."),
            _ => throw new BusinessRuleException("The requested payment method is not supported.")
        };

        var paymentWindow = TimeSpan.FromMinutes(
            expirationOptions.Value.PaymentWindowMinutes);

        var cutoffUtc = DateTime.UtcNow.Subtract(paymentWindow);

        if (order.CreatedAt <= cutoffUtc)
        {
            throw new BusinessRuleException(
                "The payment window for this order has expired.");
        }

        var activePayment = order.Payments.SingleOrDefault(
            candidate => candidate.Status == PaymentStatus.Pending);

        if (activePayment is not null)
        {
            // A repeated initiation request must return the existing checkout session
            // rather than creating another payment request or another provider charge.
            if (activePayment.PaymentMethod == PaymentMethod.ZarinPal &&
                command.PaymentMethod == PaymentMethod.ZarinPal &&
                !string.IsNullOrWhiteSpace(activePayment.Authority) &&
                paymentGateway is not null)
            {
                return new InitiatePaymentResponse(
                    activePayment.Id,
                    order.Id,
                    activePayment.Amount,
                    activePayment.PaymentMethod,
                    activePayment.Status,
                    paymentGateway.GetPaymentUrl(activePayment.Authority));
            }

            throw new BusinessRuleException(
                "An active payment attempt already exists for this order.");
        }

        var payment = order.InitiatePayment(command.PaymentMethod);

        // Persist our local payment before contacting the provider. The PaymentId is
        // then available in the callback URL and is the stable idempotency reference
        // used by this application, even though the provider does not expose a
        // request-idempotency header in its v4 sample contract.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string? redirectUrl = null;

        if (paymentGateway is not null)
        {
            PaymentGatewayRequestResult gatewayResult;

            try
            {
                gatewayResult = await paymentGateway.RequestPaymentAsync(
                    payment.Id,
                    order.Id,
                    payment.Amount,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                payment.MarkFailed();
                await unitOfWork.SaveChangesAsync(cancellationToken);

                throw new PaymentGatewayException(
                    "The payment request could not be completed. Please retry the payment.",
                    exception);
            }

            if (!gatewayResult.IsSuccessful ||
                string.IsNullOrWhiteSpace(gatewayResult.Authority) ||
                string.IsNullOrWhiteSpace(gatewayResult.PaymentUrl))
            {
                payment.MarkFailed();
                await unitOfWork.SaveChangesAsync(cancellationToken);

                throw new PaymentGatewayException(
                    "The payment provider rejected the payment request. Please retry the payment.");
            }

            payment.SetAuthority(gatewayResult.Authority);
            redirectUrl = gatewayResult.PaymentUrl;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new InitiatePaymentResponse(
            payment.Id,
            order.Id,
            payment.Amount,
            payment.PaymentMethod,
            payment.Status,
            redirectUrl);
    }
}
