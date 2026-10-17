namespace ECommerce.Application.Features.Cart.GetCart;

public sealed record GetCartResponse(
    IReadOnlyList<GetCartItemResponse> Items,
    decimal TotalAmount);

public sealed record GetCartItemResponse(
    Guid SellerOfferId,
    string ProductName,
    string? VariantName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);
