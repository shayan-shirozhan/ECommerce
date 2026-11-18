using ECommerce.Application.Features.Authentication.ResetPassword;

namespace ECommerce.Tests.Application.Features.Authentication.ResetPassword;

public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Test]
    public void Validate_should_accept_a_complete_request()
    {
        var result = _validator.Validate(
            new ResetPasswordCommand("customer@test.com", "reset-token", "NewPassword123!"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_should_reject_missing_email_token_and_new_password()
    {
        var result = _validator.Validate(new ResetPasswordCommand(string.Empty, string.Empty, string.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Select(error => error.PropertyName),
            Is.EquivalentTo(new[] { "Email", "Token", "NewPassword" }));
    }

    [TestCase("not-an-email")]
    [TestCase("customer@")]
    public void Validate_should_reject_invalid_email(string email)
    {
        var result = _validator.Validate(new ResetPasswordCommand(email, "reset-token", "NewPassword123!"));
        Assert.That(result.IsValid, Is.False);
    }
}
