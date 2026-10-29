using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetMyOrderById;

public sealed class GetMyOrderByIdQueryHandler(ICurrentUser currentUser, IOrderRepository orderRepository)
    : IRequestHandler<GetMyOrderByIdQuery, GetMyOrderByIdResponse>
{
    public async Task<GetMyOrderByIdResponse> Handle(GetMyOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");
        var order = await orderRepository.GetByIdForUserAsync(query.OrderId, userId, cancellationToken);
        if (order is null)
            throw new NotFoundException($"Order with id '{query.OrderId}' was not found.");

        return new GetMyOrderByIdResponse(order.Id, order.Status, order.CreatedAt, order.TotalAmount,
            order.ShippingRecipientName, order.ShippingPhoneNumber, order.ShippingProvince, order.ShippingCity,
            order.ShippingPostalCode, order.ShippingAddressLine, order.Groups
                .Select(orderGroup =>
                    new OrderGroupDetailsResponse(orderGroup.Id, orderGroup.SellerId, orderGroup.Status,
                        orderGroup.TotalAmount,
                        orderGroup.Items.Select(item =>
                            new OrderItemDetailsResponse(item.Id, item.ProductId, item.ProductVariantId,
                                item.SellerOfferId, item.ProductName, item.VariantName, item.SellerName, item.UnitPrice,
                                item.Quantity, item.DiscountAmount, item.TotalAmount)).ToList())).ToList());
    }
}
