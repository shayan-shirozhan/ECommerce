using FluentValidation;

namespace ECommerce.Application.Features.Orders.GetAdminOrderById;

public sealed class GetAdminOrderByIdQueryValidator : AbstractValidator<GetAdminOrderByIdQuery>
{
    public GetAdminOrderByIdQueryValidator()
    {
        RuleFor(query => query.OrderId)
            .NotEmpty()
            .WithMessage("Order ID is required.");
    }
}
