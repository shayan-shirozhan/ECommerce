using MediatR;

namespace ECommerce.Application.Features.Authentication.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Unit>;
