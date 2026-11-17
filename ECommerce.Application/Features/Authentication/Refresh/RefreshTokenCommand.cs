using MediatR;

namespace ECommerce.Application.Features.Authentication.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResponseDto>;
