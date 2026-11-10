using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed class GetMySellerOffersQueryValidatorTests
{
    private readonly GetMySellerOffersQueryValidator _validator = new();

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Validate_should_reject_invalid_page(int page)
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(Page: page));

        Assert.That(result.IsValid, Is.False);

        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetMySellerOffersQuery.Page)),
            Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public async Task Validate_should_reject_invalid_page_size(int pageSize)
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(PageSize: pageSize));

        Assert.That(result.IsValid, Is.False);

        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetMySellerOffersQuery.PageSize)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_reject_page_when_offset_exceeds_supported_range()
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(
                Page: int.MaxValue,
                PageSize: 100));

        Assert.That(result.IsValid, Is.False);

        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetMySellerOffersQuery.Page)),
            Is.True);
    }

    [TestCase((SellerOfferStatus)0)]
    [TestCase((SellerOfferStatus)99)]
    [TestCase((SellerOfferStatus)(-1))]
    public async Task Validate_should_reject_invalid_status(SellerOfferStatus status)
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(Status: status));

        Assert.That(result.IsValid, Is.False);

        Assert.That(
            result.Errors.Any(error =>
                error.PropertyName == nameof(GetMySellerOffersQuery.Status)),
            Is.True);
    }

    [TestCase(SellerOfferStatus.Active)]
    [TestCase(SellerOfferStatus.Inactive)]
    public async Task Validate_should_accept_valid_status(SellerOfferStatus status)
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(Status: status));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_null_status()
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(Status: null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_maximum_page_size()
    {
        var result = await _validator.ValidateAsync(
            new GetMySellerOffersQuery(
                PageSize: GetMySellerOffersQuery.MaxPageSize));

        Assert.That(result.IsValid, Is.True);
    }
}
