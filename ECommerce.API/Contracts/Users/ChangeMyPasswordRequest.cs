namespace ECommerce.API.Contracts.Users;

public sealed record ChangeMyPasswordRequest(
    string CurrentPassword,
    string NewPassword);
