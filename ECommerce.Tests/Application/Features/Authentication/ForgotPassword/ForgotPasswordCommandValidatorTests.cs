using ECommerce.Application.Features.Authentication.ForgotPassword;

namespace ECommerce.Tests.Application.Features.Authentication.ForgotPassword;

public sealed class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Test]
    public void Validate_should_accept_a_valid_email()
    {
        var result = _validator.Validate(new ForgotPasswordCommand("customer@test.com"));
        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("")]
    [TestCase("not-an-email")]
    [TestCase("customer@")]
    public void Validate_should_reject_invalid_email(string email)
    {
        var result = _validator.Validate(new ForgotPasswordCommand(email));
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_should_reject_email_longer_than_two_hundred_and_fifty_six_characters()
    {
        var email = $"{new string('a', 248)}@test.com";
        var result = _validator.Validate(new ForgotPasswordCommand(email));
        Assert.That(result.IsValid, Is.False);
    }
}
