using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.CancelMyOrder;

public sealed class CancelMyOrderCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelMyOrderCommand, CancelMyOrderResponse>
{
    public async Task<CancelMyOrderResponse> Handle(
        CancelMyOrderCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForUserForCancellationAsync(
            command.OrderId,
            userId,
            cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(
                $"Order with id '{command.OrderId}' was not found.");
        }

        // The domain aggregate enforces that only Pending/AwaitingPayment orders
        // with Pending seller groups can be cancelled.
        order.Cancel();

        foreach (var payment in order.Payments.Where(payment => payment.Status == PaymentStatus.Pending))
        {
            payment.Cancel();
        }

        var restoration = await OrderInventoryRestorer.RestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);

        // EF Core persists the order, its payments, and all offer stock changes
        // in one database transaction. A concurrency failure rolls everything back.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelMyOrderResponse(
            order.Id,
            order.Status,
            restoration.RestoredItemCount,
            restoration.RestoredQuantity);
    }
}
