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

        When(command => command.NewStatus == OrderGroupStatus.Shipped, () =>
        {
            RuleFor(command => command.ShippingCarrier)
                .NotEmpty()
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Shipping carrier is required when shipping an order group.")
                .MaximumLength(100)
                .WithMessage("Shipping carrier cannot exceed 100 characters.");

            RuleFor(command => command.TrackingCode)
                .NotEmpty()
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("Tracking code is required when shipping an order group.")
                .MaximumLength(100)
                .WithMessage("Tracking code cannot exceed 100 characters.");
        });

        When(command => command.NewStatus != OrderGroupStatus.Shipped, () =>
        {
            RuleFor(command => command.ShippingCarrier)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("Shipping carrier can only be supplied when moving an order group to shipped.");

            RuleFor(command => command.TrackingCode)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("Tracking code can only be supplied when moving an order group to shipped.");
        });
    }
}
