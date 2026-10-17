using MediatR;

namespace ECommerce.Application.Features.Cart.GetCart;

public sealed record GetCartQuery : IRequest<GetCartResponse>;
