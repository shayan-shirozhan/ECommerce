using MediatR;

namespace ECommerce.Application.Features.Orders.GetMyOrderById;

public sealed record GetMyOrderByIdQuery(Guid OrderId) : IRequest<GetMyOrderByIdResponse>;
