using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders;

internal static class OrderCancellationProcessor
{
    internal static async Task<OrderInventoryRestorationResult> CancelAndRestoreAsync(
        Order order,
        ISellerOfferRepository sellerOfferRepository,
        CancellationToken cancellationToken)
    {
        order.Cancel();

        foreach (var payment in order.Payments
                     .Where(payment => payment.Status == PaymentStatus.Pending))
        {
            payment.Cancel();
        }

        return await OrderInventoryRestorer.RestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);
    }
}
