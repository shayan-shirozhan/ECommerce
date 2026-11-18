using ECommerce.Application.Features.Orders.GetAdminOrders;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Orders.GetAdminOrders;

public sealed class GetAdminOrdersQueryValidatorTests
{
    private readonly GetAdminOrdersQueryValidator _validator = new();

    [Test]
    public void Validate_should_accept_default_query()
    {
        var result = _validator.Validate(new GetAdminOrdersQuery());

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(0, 10)]
    [TestCase(1, 0)]
    [TestCase(1, 101)]
    public void Validate_should_reject_invalid_page_or_page_size(int page, int pageSize)
    {
        var result = _validator.Validate(new GetAdminOrdersQuery(Page: page, PageSize: pageSize));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_should_reject_unknown_order_status()
    {
        var result = _validator.Validate(
            new GetAdminOrdersQuery(Status: (OrderStatus)999));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(error => error.PropertyName == nameof(GetAdminOrdersQuery.Status)), Is.True);
    }

    [Test]
    public void Validate_should_reject_date_range_when_from_is_after_to()
    {
        var result = _validator.Validate(new GetAdminOrdersQuery(
            CreatedFromUtc: new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
            CreatedToUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Is.Not.Empty);
    }
}
