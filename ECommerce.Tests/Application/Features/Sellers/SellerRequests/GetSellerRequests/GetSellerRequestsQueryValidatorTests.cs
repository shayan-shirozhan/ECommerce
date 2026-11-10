using ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed class GetSellerRequestsQueryValidatorTests
{
    private readonly GetSellerRequestsQueryValidator _validator = new();

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Validate_should_reject_invalid_page(int page)
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(Page: page));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public async Task Validate_should_reject_invalid_page_size(int pageSize)
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(PageSize: pageSize));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Validate_should_reject_page_when_offset_exceeds_supported_range()
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(
                Page: int.MaxValue,
                PageSize: 100));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase((SellerRequestStatus)0)]
    [TestCase((SellerRequestStatus)99)]
    [TestCase((SellerRequestStatus)(-1))]
    public async Task Validate_should_reject_invalid_status(SellerRequestStatus status)
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(Status: status));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(SellerRequestStatus.Pending)]
    [TestCase(SellerRequestStatus.Approved)]
    [TestCase(SellerRequestStatus.Rejected)]
    public async Task Validate_should_accept_valid_status(SellerRequestStatus status)
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(Status: status));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_accept_null_status()
    {
        var result = await _validator.ValidateAsync(
            new GetSellerRequestsQuery(Status: null));

        Assert.That(result.IsValid, Is.True);
    }
}
