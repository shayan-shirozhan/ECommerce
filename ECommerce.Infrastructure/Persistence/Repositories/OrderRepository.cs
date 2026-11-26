using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(ECommerceDbContext dbContext) : IOrderRepository
{
    public async Task<IReadOnlyList<AdminOrderListItemData>> GetAdminPageAsync(
        int page,
        int pageSize,
        OrderStatus? status = null,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        bool? hasManualReview = null,
        CancellationToken cancellationToken = default)
    {
        var offset = checked((int)(((long)page - 1L) * pageSize));
        var orders = ApplyAdminFilters(status, createdFromUtc, createdToUtc, hasManualReview);

        return await (
            from order in orders
            join customer in dbContext.Users.AsNoTracking()
                on order.UserId equals customer.Id
            orderby order.CreatedAt descending, order.Id descending
            select new AdminOrderListItemData(
                order.Id,
                order.UserId,
                customer.Email,
                customer.FirstName,
                customer.LastName,
                order.Status,
                order.CreatedAt,
                order.TotalAmount,
                order.Groups.Count(),
                order.Groups.SelectMany(orderGroup => orderGroup.Items).Count(),
                order.Payments.Any(payment => payment.Status == PaymentStatus.RequiresManualReview)))
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAdminAsync(
        OrderStatus? status = null,
        DateTime? createdFromUtc = null,
        DateTime? createdToUtc = null,
        bool? hasManualReview = null,
        CancellationToken cancellationToken = default)
    {
        return ApplyAdminFilters(status, createdFromUtc, createdToUtc, hasManualReview)
            .CountAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForAdminAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Where(order => order.Id == orderId)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByPaymentIdForAdminForUpdateAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsSplitQuery()
            .Where(order => order.Payments.Any(payment => payment.Id == paymentId))
            .Include(order => order.Payments)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<Order> ApplyAdminFilters(
        OrderStatus? status,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        bool? hasManualReview)
    {
        var query = dbContext.Orders.AsNoTracking();

        if (status.HasValue)
            query = query.Where(order => order.Status == status.Value);

        if (createdFromUtc.HasValue)
            query = query.Where(order => order.CreatedAt >= createdFromUtc.Value);

        if (createdToUtc.HasValue)
            query = query.Where(order => order.CreatedAt <= createdToUtc.Value);

        if (hasManualReview.HasValue)
        {
            query = hasManualReview.Value
                ? query.Where(order => order.Payments.Any(payment =>
                    payment.Status == PaymentStatus.RequiresManualReview))
                : query.Where(order => !order.Payments.Any(payment =>
                    payment.Status == PaymentStatus.RequiresManualReview));
        }

        return query;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<string?> GetSellerDisplayNameAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var seller = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == sellerId)
            .Select(user => new
            {
                user.FirstName,
                user.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (seller is null)
            return null;

        var displayName = string.Join(
            " ",
            new[] { seller.FirstName, seller.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name)));

        return string.IsNullOrWhiteSpace(displayName)
            ? null
            : displayName;
    }

    public async Task<IReadOnlyList<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForUserForUpdateAsync(Guid orderId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForUserForCancellationAsync(Guid orderId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsSplitQuery()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Include(order => order.Payments)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByPaymentIdForUserForUpdateAsync(Guid paymentId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsSplitQuery()
            .Where(order =>
                order.UserId == userId &&
                order.Payments.Any(payment => payment.Id == paymentId))
            .Include(order => order.Payments)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByPaymentIdForPaymentCallbackAsync(Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsSplitQuery()
            .Where(order => order.Payments.Any(payment => payment.Id == paymentId))
            .Include(order => order.Payments)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetBySellerIdAsync(Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Groups.Any(group => group.SellerId == sellerId))
            .Include(order => order.Groups.Where(group => group.SellerId == sellerId))
            .ThenInclude(group => group.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForSellerAsync(Guid orderId, Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.Id == orderId &&
                order.Groups.Any(group => group.SellerId == sellerId))
            .Include(order => order.Groups.Where(group => group.SellerId == sellerId))
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForSellerForUpdateAsync(Guid orderId, Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .Where(order =>
                order.Id == orderId &&
                order.Groups.Any(group => group.SellerId == sellerId))
            .Include(order => order.Groups)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetExpiredOrderIdsAsync(
        DateTime cutoffUtc,
        DateTime retryCutoffUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        return await dbContext.Orders
            .AsNoTracking()
            .Where(order =>
                order.CreatedAt <= cutoffUtc &&
                !order.Payments.Any(payment => payment.CreatedAt > cutoffUtc) &&
                (order.Status == OrderStatus.Pending ||
                 order.Status == OrderStatus.AwaitingPayment) &&
                !order.Payments.Any(payment => payment.Status == PaymentStatus.RequiresManualReview) &&
                !order.Payments.Any(payment =>
                    payment.Status == PaymentStatus.Pending &&
                    payment.PaymentMethod == PaymentMethod.ZarinPal &&
                    ((payment.Authority != null &&
                      payment.LastVerificationAttemptedAt > retryCutoffUtc) ||
                     (payment.Authority == null &&
                      payment.LastGatewayRequestStartedAt > retryCutoffUtc))))
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Select(order => order.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingZarinPalPaymentData>> GetPendingZarinPalPaymentsForReconciliationAsync(
        DateTime retryCutoffUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (retryCutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Retry cutoff must be UTC.", nameof(retryCutoffUtc));

        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take));

        return await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.Status == PaymentStatus.Pending &&
                payment.PaymentMethod == PaymentMethod.ZarinPal &&
                payment.Authority != null &&
                payment.LastVerificationAttemptedAt != null &&
                payment.LastVerificationAttemptedAt <= retryCutoffUtc &&
                dbContext.Orders.Any(order =>
                    order.Id == payment.OrderId &&
                    order.Status == OrderStatus.AwaitingPayment))
            .OrderBy(payment => payment.LastVerificationAttemptedAt)
            .ThenBy(payment => payment.Id)
            .Select(payment => new PendingZarinPalPaymentData(
                payment.Id,
                payment.Authority!))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForExpirationAsync(Guid orderId, DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsSplitQuery()
            .Where(order =>
                order.Id == orderId &&
                order.CreatedAt <= cutoffUtc &&
                !order.Payments.Any(payment => payment.CreatedAt > cutoffUtc) &&
                (order.Status == OrderStatus.Pending ||
                 order.Status == OrderStatus.AwaitingPayment) &&
                !order.Payments.Any(payment => payment.Status == PaymentStatus.RequiresManualReview))
            .Include(order => order.Payments)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
