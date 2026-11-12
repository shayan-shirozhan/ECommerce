namespace ECommerce.Application.Features.Addresses.CreateAddress;

public sealed record CreateAddressResponse(
    Guid Id,
    bool IsDefault,
    DateTime CreatedAt);
