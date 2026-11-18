using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public sealed record UserProfileData(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    SellerStatus SellerStatus);
