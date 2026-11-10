using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed record GetSellerRequestsResponse(
    IReadOnlyList<SellerRequestListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SellerRequestListItemResponse(
    Guid Id,
    Guid UserId,
    SellerRequestStatus Status,
    string? Reason,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt);
