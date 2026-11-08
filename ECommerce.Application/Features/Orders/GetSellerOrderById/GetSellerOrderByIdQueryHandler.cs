using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetSellerOrderById;

public sealed class GetSellerOrderByIdQueryHandler(IOrderRepository orderRepository, ICurrentUser currentUser) : IRequestHandler<GetSellerOrderByIdQuery,GetSellerOrderByIdResponse>
{
    public async Task<GetSellerOrderByIdResponse> Handle(GetSellerOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForSellerAsync(query.OrderId, userId, cancellationToken);
        if (order is null)
            throw new NotFoundException($"Order with id '{query.OrderId}' was not found.");

        var orderGroup = order.Groups.SingleOrDefault();
        if (orderGroup is null)
            throw new NotFoundException("Seller order group was not found.");

        return new GetSellerOrderByIdResponse(order.Id, order.Status, order.CreatedAt, order.ShippingRecipientName,
            order.ShippingPhoneNumber, order.ShippingProvince, order.ShippingCity, order.ShippingPostalCode,
            order.ShippingAddressLine, orderGroup.Id, orderGroup.Status, orderGroup.TotalAmount,
            orderGroup.Items.Select(item => new SellerOrderItemResponse(item.Id, item.ProductId, item.ProductVariantId,
                item.SellerOfferId, item.ProductName, item.VariantName, item.SellerName, item.UnitPrice, item.Quantity,
                item.DiscountAmount, item.TotalAmount)).ToList());
    }
}
