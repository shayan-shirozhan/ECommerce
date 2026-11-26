using ECommerce.Application.Features.Payments.ResolveAdminPayment;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Payments.ResolveAdminPayment;

public sealed class ResolveAdminPaymentCommandValidatorTests
{
    private readonly ResolveAdminPaymentCommandValidator _validator = new();

    [Test]
    public void Validate_should_accept_approval_with_positive_reference_and_reason()
    {
        var result = _validator.Validate(new ResolveAdminPaymentCommand(
            Guid.NewGuid(),
            PaymentResolutionAction.Approve,
            "Matched provider settlement.",
            123456789));

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(PaymentResolutionAction.VerifyAgain)]
    [TestCase(PaymentResolutionAction.RejectAndCancel)]
    public void Validate_should_reject_reference_for_actions_that_do_not_use_it(
        PaymentResolutionAction action)
    {
        var result = _validator.Validate(new ResolveAdminPaymentCommand(
            Guid.NewGuid(), action, "Reviewed provider evidence.", 123456789));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(null)]
    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_should_reject_missing_or_non_positive_approval_reference(long? refId)
    {
        var result = _validator.Validate(new ResolveAdminPaymentCommand(
            Guid.NewGuid(),
            PaymentResolutionAction.Approve,
            "Matched provider settlement.",
            refId));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_should_reject_empty_payment_id_and_reason()
    {
        var result = _validator.Validate(new ResolveAdminPaymentCommand(
            Guid.Empty,
            PaymentResolutionAction.VerifyAgain,
            " "));

        Assert.That(result.IsValid, Is.False);
    }
}
