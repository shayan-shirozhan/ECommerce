using FluentValidation;

namespace ECommerce.Application.Features.Orders.CancelMyOrder;

public sealed class CancelMyOrderCommandValidator : AbstractValidator<CancelMyOrderCommand>
{
    public CancelMyOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
    }
}
