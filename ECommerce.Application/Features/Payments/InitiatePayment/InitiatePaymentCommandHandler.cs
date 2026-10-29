using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Payments.InitiatePayment;

public sealed class InitiatePaymentCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    public async Task<InitiatePaymentResponse> Handle(InitiatePaymentCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForUserForUpdateAsync(command.OrderId, userId, cancellationToken);

        if (order is null)
            throw new NotFoundException($"Order with id '{command.OrderId}' was not found.");

        var payment = order.InitiatePayment();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new InitiatePaymentResponse(
            payment.Id,
            order.Id,
            payment.Amount,
            payment.PaymentMethod,
            payment.Status);
    }
}
