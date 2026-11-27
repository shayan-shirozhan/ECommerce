namespace ECommerce.Infrastructure.Persistence.Seed;

/// <summary>
/// Credentials used only when bootstrapping the isolated public Demo database.
/// Never commit actual values; provide them through deployment secrets or environment variables.
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public const string ValidationErrorMessage =
        "DemoSeed requires four distinct passwords (AdminPassword, SellerPassword, SecondSellerPassword, and CustomerPassword). " +
        "Each password must be at least 8 characters and contain an uppercase letter, a lowercase letter, a digit, and a non-alphanumeric character.";

    public string AdminPassword { get; set; } = string.Empty;

    public string SellerPassword { get; set; } = string.Empty;

    public string SecondSellerPassword { get; set; } = string.Empty;

    public string CustomerPassword { get; set; } = string.Empty;

    public bool HasValidPasswords
    {
        get
        {
            var passwords = new[]
            {
                AdminPassword,
                SellerPassword,
                SecondSellerPassword,
                CustomerPassword
            };

            return passwords.All(IsValidPassword) &&
                   passwords.Distinct(StringComparer.Ordinal).Count() == passwords.Length;
        }
    }

    public void Validate()
    {
        if (!HasValidPasswords)
            throw new InvalidOperationException(ValidationErrorMessage);
    }

    private static bool IsValidPassword(string? password)
        => !string.IsNullOrWhiteSpace(password) &&
           password.Length >= 8 &&
           password.Any(char.IsUpper) &&
           password.Any(char.IsLower) &&
           password.Any(char.IsDigit) &&
           password.Any(character => !char.IsLetterOrDigit(character));
}
