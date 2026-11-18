using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetAdminOrderById;

public sealed class GetAdminOrderByIdQueryHandler(
    IOrderRepository orderRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetAdminOrderByIdQuery, GetAdminOrderByIdResponse>
{
    public async Task<GetAdminOrderByIdResponse> Handle(
        GetAdminOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdForAdminAsync(
            query.OrderId,
            cancellationToken);

        if (order is null)
            throw new NotFoundException($"Order with id '{query.OrderId}' was not found.");

        var customer = await userRepository.GetProfileAsync(
            order.UserId,
            cancellationToken);

        if (customer is null)
            throw new NotFoundException($"Customer with id '{order.UserId}' was not found.");

        var groups = order.Groups
            .OrderBy(group => group.CreatedAt)
            .Select(group => new AdminOrderGroupResponse(
                group.Id,
                group.SellerId,
                group.Items.Select(item => item.SellerName).FirstOrDefault() ?? string.Empty,
                group.Status,
                group.TotalAmount,
                group.ShippingCarrier,
                group.TrackingCode,
                group.Items
                    .Select(item => new AdminOrderItemResponse(
                        item.Id,
                        item.ProductId,
                        item.ProductVariantId,
                        item.SellerOfferId,
                        item.ProductName,
                        item.VariantName,
                        item.SellerName,
                        item.UnitPrice,
                        item.Quantity,
                        item.DiscountAmount,
                        item.TotalAmount))
                    .ToList()))
            .ToList();

        var payments = order.Payments
            .OrderBy(payment => payment.CreatedAt)
            .Select(payment => new AdminOrderPaymentResponse(
                payment.Id,
                payment.PaymentMethod,
                payment.Status,
                payment.Amount,
                payment.RefId,
                payment.CreatedAt,
                payment.PaidAt,
                payment.FailedAt))
            .ToList();

        return new GetAdminOrderByIdResponse(
            order.Id,
            order.Status,
            order.CreatedAt,
            order.UpdatedAt,
            order.TotalAmount,
            new AdminOrderCustomerResponse(
                customer.Id,
                customer.Email,
                customer.FirstName,
                customer.LastName),
            new AdminOrderShippingAddressResponse(
                order.ShippingRecipientName,
                order.ShippingPhoneNumber,
                order.ShippingProvince,
                order.ShippingCity,
                order.ShippingPostalCode,
                order.ShippingAddressLine),
            groups,
            payments);
    }
}
