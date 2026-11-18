using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Application.Features.Authentication.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IIdentityService identityService,
    IEmailSender emailSender,
    IOptions<PasswordResetEmailOptions> options,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    private const string GenericMessage =
        "If an account exists for this email address, password reset instructions will be sent.";

    public async Task<ForgotPasswordResponse> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        var token = await identityService.GeneratePasswordResetTokenAsync(email, cancellationToken);

        if (token is not null)
        {
            if (string.IsNullOrWhiteSpace(options.Value.FrontendUrl))
            {
                logger.LogError(
                    "Password reset email delivery was skipped because PasswordResetEmail:FrontendUrl is not configured.");
                return new ForgotPasswordResponse(GenericMessage);
            }

            var resetLink = BuildResetLink(options.Value.FrontendUrl, email, token);
            var message = new EmailMessage(
                email,
                "Reset your ECommerce password",
                $"Use the following link to reset your password. This link expires in {options.Value.TokenLifespanMinutes} minutes.\n\n{resetLink}");

            try
            {
                await emailSender.SendAsync(message, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Keep the response identical for existing and unknown accounts to prevent email enumeration.
                logger.LogError(exception, "Password reset email delivery failed.");
            }
        }

        return new ForgotPasswordResponse(GenericMessage);
    }

    private static string BuildResetLink(string frontendUrl, string email, string token)
    {
        var separator = frontendUrl.Contains('?') ? "&" : "?";
        return $"{frontendUrl}{separator}email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }
}
