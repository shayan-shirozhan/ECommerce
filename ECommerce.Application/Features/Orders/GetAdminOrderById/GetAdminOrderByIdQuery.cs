using MediatR;

namespace ECommerce.Application.Features.Orders.GetAdminOrderById;

public sealed record GetAdminOrderByIdQuery(Guid OrderId)
    : IRequest<GetAdminOrderByIdResponse>;
