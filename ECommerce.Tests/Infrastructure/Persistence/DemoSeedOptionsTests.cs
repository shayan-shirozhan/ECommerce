using ECommerce.Infrastructure.Persistence.Seed;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class DemoSeedOptionsTests
{
    [Test]
    public void HasValidPasswords_AcceptsFourDistinctIdentityCompliantPasswords()
    {
        var options = CreateValidOptions();

        Assert.That(options.HasValidPasswords, Is.True);
        Assert.DoesNotThrow(options.Validate);
    }

    [Test]
    public void Validate_RejectsMissingPasswords()
    {
        var options = CreateValidOptions();
        options.CustomerPassword = "";

        Assert.That(options.HasValidPasswords, Is.False);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Test]
    public void Validate_RejectsPasswordsThatDoNotMeetIdentityRequirements()
    {
        var options = CreateValidOptions();
        options.CustomerPassword = "lowercase-without-digit-or-uppercase!";

        Assert.That(options.HasValidPasswords, Is.False);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Test]
    public void Validate_RejectsReusedPasswordsBetweenDemoAccounts()
    {
        var options = CreateValidOptions();
        options.SecondSellerPassword = options.SellerPassword;

        Assert.That(options.HasValidPasswords, Is.False);
        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static DemoSeedOptions CreateValidOptions()
        => new()
        {
            AdminPassword = "Admin-Demo-7f!x",
            SellerPassword = "Seller-Demo-8g!y",
            SecondSellerPassword = "Second-Demo-9h!z",
            CustomerPassword = "Customer-Demo-4j!q"
        };
}
