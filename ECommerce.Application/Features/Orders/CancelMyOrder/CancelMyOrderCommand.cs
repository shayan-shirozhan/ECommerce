using MediatR;

namespace ECommerce.Application.Features.Orders.CancelMyOrder;

public sealed record CancelMyOrderCommand(Guid OrderId)
    : IRequest<CancelMyOrderResponse>;
