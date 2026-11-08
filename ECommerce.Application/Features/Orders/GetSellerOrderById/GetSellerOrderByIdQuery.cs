using MediatR;

namespace ECommerce.Application.Features.Orders.GetSellerOrderById;

public sealed record GetSellerOrderByIdQuery(Guid OrderId) : IRequest<GetSellerOrderByIdResponse>;
