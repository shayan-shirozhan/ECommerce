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

        ECommerce.Domain.Entities.Payment payment;

        if (activePayment is not null)
        {
            // A persisted authority is a complete checkout session. Repeated requests
            // must return it and must not create another provider session.
            if (activePayment.PaymentMethod == PaymentMethod.ZarinPal &&
                command.PaymentMethod == PaymentMethod.ZarinPal &&
                paymentGateway is not null &&
                !string.IsNullOrWhiteSpace(activePayment.Authority))
            {
                return new InitiatePaymentResponse(
                    activePayment.Id,
                    order.Id,
                    activePayment.Amount,
                    activePayment.PaymentMethod,
                    activePayment.Status,
                    paymentGateway.GetPaymentUrl(activePayment.Authority));
            }

            // A request may have reached ZarinPal even when our process did not receive
            // its response. Keep that payment Pending and retry the same local PaymentId
            // only after the short lease expires. The provider's v4 request sample does
            // not define an idempotency key; an unknown authority is never returned to
            // the client, so an orphaned checkout session cannot be used by this app.
            // Payment.RowVersion makes the retry claim atomic across concurrent requests.
            if (activePayment.PaymentMethod != PaymentMethod.ZarinPal ||
                command.PaymentMethod != PaymentMethod.ZarinPal ||
                paymentGateway is null)
            {
                throw new BusinessRuleException(
                    "An active payment attempt already exists for this order.");
            }

            if (!PaymentGatewayRetryPolicy.CanRetry(
                    activePayment.LastGatewayRequestStartedAt,
                    DateTime.UtcNow))
            {
                throw new BusinessRuleException(
                    "The payment request is still being processed. Please retry in two minutes.");
            }

            payment = activePayment;
        }
        else
        {
            payment = order.InitiatePayment(command.PaymentMethod);
        }

        string? redirectUrl = null;

        if (paymentGateway is not null)
        {
            payment.MarkGatewayRequestStarted(DateTime.UtcNow);
        }

        // Persist the PaymentId and request lease before contacting the provider.
        // A failed/ambiguous HTTP exchange can then be retried using this same payment.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (paymentGateway is not null)
        {
            var gatewayResult = await paymentGateway.RequestPaymentAsync(
                payment.Id,
                order.Id,
                payment.Amount,
                cancellationToken);

            if (!gatewayResult.IsSuccessful)
            {
                if (gatewayResult.IsIndeterminate)
                {
                    // Do not close a payment when the provider response does not prove
                    // that checkout-session creation failed. Keep the same payment for
                    // a later recovery attempt after the request lease expires.
                    throw new PaymentGatewayException(
                        "The payment provider returned an indeterminate request result. " +
                        "The payment remains pending and can be retried after two minutes.");
                }

                // A structured provider rejection is definitive: no checkout session
                // was created, so this attempt can be closed and a new one may be made.
                payment.MarkFailed();
                await unitOfWork.SaveChangesAsync(cancellationToken);

                throw new PaymentGatewayException(
                    "The payment provider rejected the payment request. Please retry the payment.");
            }

            // A success flag without the authority needed for redirect is not a safe,
            // definitive rejection. Leave the attempt pending for recovery instead.
            if (string.IsNullOrWhiteSpace(gatewayResult.Authority) ||
                string.IsNullOrWhiteSpace(gatewayResult.PaymentUrl))
            {
                throw new PaymentGatewayException(
                    "The payment provider returned an incomplete payment request response. " +
                    "The payment attempt remains pending and can be retried after two minutes.");
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
