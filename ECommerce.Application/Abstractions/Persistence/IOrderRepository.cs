using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IOrderRepository
{
    Task<IReadOnlyList<AdminOrderListItemData>> GetAdminPageAsync(
        int page,
        int pageSize,
        OrderStatus? status = null,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        bool? hasManualReview = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAdminAsync(
        OrderStatus? status = null,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        bool? hasManualReview = null,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForAdminAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByPaymentIdForAdminForUpdateAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<string?> GetSellerDisplayNameAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForUserForUpdateAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForUserForCancellationAsync(Guid orderId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByPaymentIdForUserForUpdateAsync(Guid paymentId, Guid userId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByPaymentIdForPaymentCallbackAsync(Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForSellerAsync(Guid orderId, Guid sellerId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForSellerForUpdateAsync(Guid orderId, Guid sellerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetExpiredOrderIdsAsync(
        DateTime cutoffUtc,
        DateTime retryCutoffUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingZarinPalPaymentData>> GetPendingZarinPalPaymentsForReconciliationAsync(
        DateTime retryCutoffUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForExpirationAsync(Guid orderId, DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}
