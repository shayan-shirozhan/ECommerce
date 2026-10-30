using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.CompleteMockPayment;

public sealed class CompleteMockPaymentCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    IPaymentGateway paymentGateway,
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
            // Defensive check: the repository filters by PaymentId and includes Payments.
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
            payment.MockFailed();
            order.Cancel();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Return the final state held by the domain entity, not the raw gateway result.
        return new CompleteMockPaymentResponse(
            payment.Id,
            order.Id,
            payment.Status,
            order.Status,
            payment.TransactionId,
            payment.GatewayReference);
    }
}
