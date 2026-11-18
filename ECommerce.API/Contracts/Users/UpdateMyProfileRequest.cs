namespace ECommerce.API.Contracts.Users;

public sealed record UpdateMyProfileRequest(
    string FirstName,
    string LastName);
