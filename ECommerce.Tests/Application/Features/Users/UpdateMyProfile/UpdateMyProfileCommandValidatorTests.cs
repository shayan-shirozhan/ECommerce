using ECommerce.Application.Features.Users.UpdateMyProfile;

namespace ECommerce.Tests.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileCommandValidatorTests
{
    private readonly UpdateMyProfileCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_valid_profile_names()
    {
        var result = await _validator.ValidateAsync(new UpdateMyProfileCommand("Shayan", "Shirozhan"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_blank_profile_names()
    {
        var result = await _validator.ValidateAsync(new UpdateMyProfileCommand(" ", " "));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Validate_should_reject_profile_names_longer_than_registration_limits()
    {
        var result = await _validator.ValidateAsync(
            new UpdateMyProfileCommand(new string('f', 101), new string('l', 101)));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
    }
}
