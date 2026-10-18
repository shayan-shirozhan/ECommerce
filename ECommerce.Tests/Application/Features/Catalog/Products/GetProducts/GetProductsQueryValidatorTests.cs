using ECommerce.Application.Features.Catalog.Products.GetProducts;

namespace ECommerce.Tests.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _validator = new();

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Validate_should_reject_invalid_page(int page)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(page, 10));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.Page)),
            Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public async Task Validate_should_reject_invalid_page_size(int pageSize)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(1, pageSize));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.PageSize)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_page_when_offset_exceeds_supported_range()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(int.MaxValue, 100));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.Page)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_accept_valid_query()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(2, 10));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_maximum_page_size()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(1, GetProductsQuery.MaxPageSize));

        Assert.That(result.IsValid, Is.True);
    }
}
