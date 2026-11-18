using ECommerce.Application.Features.Orders.GetAdminOrderById;

namespace ECommerce.Tests.Application.Features.Orders.GetAdminOrderById;

public sealed class GetAdminOrderByIdQueryValidatorTests
{
    [Test]
    public void Validate_should_reject_empty_order_id()
    {
        var validator = new GetAdminOrderByIdQueryValidator();

        var result = validator.Validate(new GetAdminOrderByIdQuery(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_should_accept_non_empty_order_id()
    {
        var validator = new GetAdminOrderByIdQueryValidator();

        var result = validator.Validate(new GetAdminOrderByIdQuery(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }
}
