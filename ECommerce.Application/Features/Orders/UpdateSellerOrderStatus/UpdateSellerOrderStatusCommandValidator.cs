using ECommerce.Domain.Enums;
using FluentValidation;

namespace ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;

public sealed class UpdateSellerOrderStatusCommandValidator
    : AbstractValidator<UpdateSellerOrderStatusCommand>
{
    public UpdateSellerOrderStatusCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.NewStatus)
            .Must(status =>
                status is OrderGroupStatus.Processing or OrderGroupStatus.Shipped or OrderGroupStatus.Delivered)
            .WithMessage("Requested order group status is invalid.");
    }
}
