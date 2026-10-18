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

    [TestCase("newest")]
    [TestCase("price-asc")]
    [TestCase("price-desc")]
    [TestCase("PRICE-ASC")]
    public async Task Validate_should_accept_supported_sort_orders(string sortBy)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(SortBy: sortBy));

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase("")]
    [TestCase("invalid")]
    [TestCase("price")]
    public async Task Validate_should_reject_unsupported_sort_orders(string sortBy)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(SortBy: sortBy));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.SortBy)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_category_id()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(CategoryId: Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.CategoryId)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_brand_id()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(BrandId: Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.BrandId)),
            Is.True);
    }

    [TestCase(-1)]
    [TestCase(-0.01)]
    public async Task Validate_should_reject_negative_minimum_price(double price)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(MinPrice: (decimal)price));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.MinPrice)),
            Is.True);
    }

    [TestCase(-1)]
    [TestCase(-0.01)]
    public async Task Validate_should_reject_negative_maximum_price(double price)
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(MaxPrice: (decimal)price));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.MaxPrice)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_minimum_price_greater_than_maximum_price()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(MinPrice: 200m, MaxPrice: 100m));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.MinPrice)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_search_text_longer_than_100_characters()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(Search: new string('a', 101)));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetProductsQuery.Search)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_accept_valid_filters()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(
                Page: 2,
                PageSize: 20,
                CategoryId: Guid.NewGuid(),
                BrandId: Guid.NewGuid(),
                MinPrice: 100m,
                MaxPrice: 500m,
                Search: "Samsung",
                SortBy: ProductSortOptions.PriceAscending));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_maximum_page_size()
    {
        var result = await _validator.ValidateAsync(
            new GetProductsQuery(
                PageSize: GetProductsQuery.MaxPageSize));

        Assert.That(result.IsValid, Is.True);
    }
}
