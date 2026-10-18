using ECommerce.Application.Features.Catalog.Products.UpdateProduct;

namespace ECommerce.Tests.Application.Features.Catalog.Products.UpdateProduct;

public sealed class UpdateProductCommandValidatorTests
{
    private readonly UpdateProductCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_valid_command()
    {
        var result = await _validator.ValidateAsync(CreateValidCommand());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_product_id()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Id = Guid.Empty });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.Id)), Is.True);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Validate_should_reject_empty_name(string name)
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Name = name });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.Name)), Is.True);
    }

    [Test]
    public async Task Validate_should_reject_name_longer_than_250_characters()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Name = new string('a', 251) });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.Name)), Is.True);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Validate_should_reject_empty_description(string description)
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { Description = description });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.Description)), Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_brand_id()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { BrandId = Guid.Empty });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.BrandId)), Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_category_id()
    {
        var result = await _validator.ValidateAsync(
            CreateValidCommand() with { CategoryId = Guid.Empty });

        Assert.That(result.Errors.Any(error =>
            error.PropertyName == nameof(UpdateProductCommand.CategoryId)), Is.True);
    }

    private static UpdateProductCommand CreateValidCommand() =>
        new(
            Guid.NewGuid(),
            "Updated phone",
            "Updated description",
            Guid.NewGuid(),
            Guid.NewGuid());
}
