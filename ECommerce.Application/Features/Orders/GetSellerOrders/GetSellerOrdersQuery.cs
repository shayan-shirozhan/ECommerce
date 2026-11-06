using MediatR;

namespace ECommerce.Application.Features.Orders.GetSellerOrders;

public sealed record GetSellerOrdersQuery : IRequest<GetSellerOrdersResponse>;
