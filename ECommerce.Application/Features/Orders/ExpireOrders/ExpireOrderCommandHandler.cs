using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Orders.ExpireOrders;

public sealed class ExpireOrderCommandHandler(
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ExpireOrderCommand, bool>
{
    public async Task<bool> Handle(
        ExpireOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdForExpirationAsync(
            command.OrderId,
            command.CutoffUtc,
            cancellationToken);

        if (order is null)
            return false;

        await OrderCancellationProcessor.CancelAndRestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
