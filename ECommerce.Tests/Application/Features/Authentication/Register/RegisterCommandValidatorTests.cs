using ECommerce.Application.Features.Authentication.Register;

namespace ECommerce.Tests.Application.Features.Authentication.Register;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Test]
    public void RegisterValidator_should_accept_valid_command()
    {
        var result = _validator.Validate(
            new RegisterCommand("customer@test.com", "Password123!", "Test", "Customer"));

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("")]
    [TestCase("not-an-email")]
    [TestCase("customer@")]
    public void RegisterValidator_should_reject_invalid_email(string email)
    {
        var result = _validator.Validate(
            new RegisterCommand(email, "Password123!", "Test", "Customer"));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("")]
    [TestCase("Pass1!")]
    [TestCase("password123!")]
    [TestCase("PASSWORD123!")]
    [TestCase("Password!!!!")]
    [TestCase("Password1234")]
    public void RegisterValidator_should_reject_passwords_that_do_not_meet_policy(string password)
    {
        var result = _validator.Validate(
            new RegisterCommand("customer@test.com", password, "Test", "Customer"));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void RegisterValidator_should_reject_empty_names()
    {
        var result = _validator.Validate(
            new RegisterCommand("customer@test.com", "Password123!", " ", ""));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void RegisterValidator_should_reject_names_longer_than_one_hundred_characters()
    {
        var result = _validator.Validate(
            new RegisterCommand(
                "customer@test.com",
                "Password123!",
                new string('a', 101),
                "Customer"));

        Assert.That(result.IsValid, Is.False);
    }
}
