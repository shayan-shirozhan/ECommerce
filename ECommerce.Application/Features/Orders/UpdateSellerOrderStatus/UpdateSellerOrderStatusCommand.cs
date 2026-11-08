using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;

public sealed record UpdateSellerOrderStatusCommand(Guid OrderId, OrderGroupStatus NewStatus)
    : IRequest<UpdateSellerOrderStatusResponse>;
