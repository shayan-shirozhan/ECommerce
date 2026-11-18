using MediatR;

namespace ECommerce.Application.Features.Authentication.ResetPassword;

public sealed record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword) : IRequest;
