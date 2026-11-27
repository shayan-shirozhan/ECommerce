namespace ECommerce.Application.Exceptions;

public sealed class RefreshTokenRotationInProgressException()
    : Exception("Another request has already rotated this refresh token. Retry with the latest refresh cookie.");
