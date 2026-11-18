using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Users.GetMyProfile;

public sealed record GetMyProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    SellerStatus SellerStatus);
