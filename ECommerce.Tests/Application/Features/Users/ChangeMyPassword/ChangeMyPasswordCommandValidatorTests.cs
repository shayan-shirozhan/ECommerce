using ECommerce.Application.Features.Users.ChangeMyPassword;

namespace ECommerce.Tests.Application.Features.Users.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandValidatorTests
{
    private readonly ChangeMyPasswordCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_valid_different_passwords()
    {
        var result = await _validator.ValidateAsync(
            new ChangeMyPasswordCommand("Password123!", "NewPassword123!"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_passwords()
    {
        var result = await _validator.ValidateAsync(
            new ChangeMyPasswordCommand(string.Empty, string.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Validate_should_reject_new_password_equal_to_current_password()
    {
        var result = await _validator.ValidateAsync(
            new ChangeMyPasswordCommand("Password123!", "Password123!"));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(ChangeMyPasswordCommand.NewPassword)));
    }
}
