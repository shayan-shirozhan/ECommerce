using ECommerce.Application.Features.Catalog.Brands.DeleteBrand;
using ECommerce.Application.Features.Catalog.Brands.UpdateBrand;
using ECommerce.Application.Features.Catalog.Categories.DeleteCategory;
using ECommerce.Application.Features.Catalog.Categories.UpdateCategory;

namespace ECommerce.Tests.Application.Features.Catalog.Management;

public sealed class CatalogManagementCommandValidatorTests
{
    [Test]
    public async Task UpdateBrandValidator_should_reject_empty_id()
    {
        var validator = new UpdateBrandCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateBrandCommand(Guid.Empty, "Samsung"));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task UpdateBrandValidator_should_reject_empty_name(string name)
    {
        var validator = new UpdateBrandCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateBrandCommand(Guid.NewGuid(), name));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(UpdateBrandCommand.Name)),
            Is.True);
    }

    [Test]
    public async Task UpdateBrandValidator_should_reject_name_longer_than_150_characters()
    {
        var validator = new UpdateBrandCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateBrandCommand(Guid.NewGuid(), new string('a', 151)));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task DeleteBrandValidator_should_reject_empty_id()
    {
        var validator = new DeleteBrandCommandValidator();

        var result = await validator.ValidateAsync(
            new DeleteBrandCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task UpdateCategoryValidator_should_reject_empty_id()
    {
        var validator = new UpdateCategoryCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateCategoryCommand(Guid.Empty, "Phones", null));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task UpdateCategoryValidator_should_reject_self_parent()
    {
        var validator = new UpdateCategoryCommandValidator();
        var categoryId = Guid.NewGuid();

        var result = await validator.ValidateAsync(
            new UpdateCategoryCommand(
                categoryId,
                "Phones",
                categoryId));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName ==
                nameof(UpdateCategoryCommand.ParentCategoryId)),
            Is.True);
    }

    [Test]
    public async Task UpdateCategoryValidator_should_accept_root_category()
    {
        var validator = new UpdateCategoryCommandValidator();

        var result = await validator.ValidateAsync(
            new UpdateCategoryCommand(
                Guid.NewGuid(),
                "Phones",
                null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task DeleteCategoryValidator_should_reject_empty_id()
    {
        var validator = new DeleteCategoryCommandValidator();

        var result = await validator.ValidateAsync(
            new DeleteCategoryCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }
}
