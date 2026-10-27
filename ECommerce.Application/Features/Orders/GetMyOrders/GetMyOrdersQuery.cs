using MediatR;

namespace ECommerce.Application.Features.Orders.GetMyOrders;

public sealed record GetMyOrdersQuery : IRequest<GetMyOrdersResponse>;
