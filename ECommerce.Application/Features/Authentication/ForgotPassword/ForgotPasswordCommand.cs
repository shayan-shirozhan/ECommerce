using MediatR;

namespace ECommerce.Application.Features.Authentication.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResponse>;
