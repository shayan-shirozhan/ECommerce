using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface ISellerOfferRepository
{
    Task AddAsync(SellerOffer sellerOffer, CancellationToken cancellationToken = default);
    Task<SellerOffer?> GetByIdAsync(Guid sellerOfferId, CancellationToken cancellationToken = default);

    Task<SellerOffer?> GetByIdForUpdateAsync(Guid sellerOfferId, uint expectedRowVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SellerOfferListingItem>> GetBySellerIdPageAsync(Guid sellerId, int page, int pageSize,
        SellerOfferStatus? status = null, CancellationToken cancellationToken = default);

    Task<int> CountBySellerIdAsync(Guid sellerId, SellerOfferStatus? status = null,
        CancellationToken cancellationToken = default);
}
