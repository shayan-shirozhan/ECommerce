using MediatR;

namespace ECommerce.Application.Features.Authentication.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<RegisterResponseDto>;
