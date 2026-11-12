using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Features.Orders;

internal static class OrderInventoryRestorer
{
    internal static async Task<OrderInventoryRestorationResult> RestoreAsync(
        Order order,
        ISellerOfferRepository sellerOfferRepository,
        CancellationToken cancellationToken)
    {
        var items = order.Groups
            .SelectMany(group => group.Items)
            .ToArray();

        var quantitiesByOffer = items
            .GroupBy(item => item.SellerOfferId)
            .Select(group => new
            {
                SellerOfferId = group.Key,
                Quantity = group.Sum(item => (long)item.Quantity)
            })
            .ToArray();

        var offers = await sellerOfferRepository.GetByIdsForUpdateAsync(
            quantitiesByOffer.Select(group => group.SellerOfferId).ToArray(),
            cancellationToken);
        var offersById = offers.ToDictionary(offer => offer.Id);

        foreach (var itemGroup in quantitiesByOffer)
        {
            if (!offersById.TryGetValue(itemGroup.SellerOfferId, out var sellerOffer))
            {
                throw new BusinessRuleException(
                    $"Cannot restore stock because seller offer '{itemGroup.SellerOfferId}' no longer exists.");
            }

            var restoredStock = sellerOffer.Stock + itemGroup.Quantity;
            if (restoredStock > int.MaxValue)
            {
                throw new BusinessRuleException(
                    $"Restoring stock for seller offer '{itemGroup.SellerOfferId}' exceeds the supported stock limit.");
            }

            sellerOffer.UpdateStock((int)restoredStock);
        }

        return new OrderInventoryRestorationResult(
            items.Length,
            items.Sum(item => (long)item.Quantity));
    }
}

internal sealed record OrderInventoryRestorationResult(
    int RestoredItemCount,
    long RestoredQuantity);
