using MediatR;

namespace ECommerce.Application.Features.Orders.ExpireOrders;

public sealed record ExpireOrderCommand(
    Guid OrderId,
    DateTime CutoffUtc) : IRequest<bool>;
