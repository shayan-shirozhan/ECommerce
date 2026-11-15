using ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

namespace ECommerce.Tests.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

public sealed class UpdateProductVariantCommandValidatorTests
{
    private readonly UpdateProductVariantCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_valid_command()
    {
        var result = await _validator.ValidateAsync(CreateValidCommand());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_null_name()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Name = null });

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_id()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Id = Guid.Empty });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductVariantCommand.Id)), Is.True);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Validate_should_reject_empty_sku(string sku)
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { SKU = sku });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductVariantCommand.SKU)), Is.True);
    }

    [Test]
    public async Task Validate_should_reject_sku_longer_than_100_characters()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { SKU = new string('a', 101) });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductVariantCommand.SKU)), Is.True);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Validate_should_reject_empty_non_null_name(string name)
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Name = name });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductVariantCommand.Name)), Is.True);
    }

    [Test]
    public async Task Validate_should_reject_name_longer_than_150_characters()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Name = new string('a', 151) });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductVariantCommand.Name)), Is.True);
    }

    private static UpdateProductVariantCommand CreateValidCommand() =>
        new(Guid.NewGuid(), "SKU-UPDATED", "Updated name");
}
