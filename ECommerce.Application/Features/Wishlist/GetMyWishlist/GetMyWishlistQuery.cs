using MediatR;

namespace ECommerce.Application.Features.Wishlist.GetMyWishlist;

public sealed record GetMyWishlistQuery : IRequest<GetMyWishlistResponse>;
