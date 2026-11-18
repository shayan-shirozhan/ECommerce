using FluentValidation;

namespace ECommerce.Application.Features.Orders.GetAdminOrders;

public sealed class GetAdminOrdersQueryValidator : AbstractValidator<GetAdminOrdersQuery>
{
    public GetAdminOrdersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .Must((query, page) =>
                ((long)page - 1L) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, GetAdminOrdersQuery.MaxPageSize)
            .WithMessage(
                $"Page size must be between 1 and {GetAdminOrdersQuery.MaxPageSize}.");

        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Status is invalid.");

        RuleFor(query => query)
            .Must(query =>
                !query.CreatedFromUtc.HasValue ||
                !query.CreatedToUtc.HasValue ||
                query.CreatedFromUtc.Value <= query.CreatedToUtc.Value)
            .WithMessage("CreatedFromUtc must be earlier than or equal to CreatedToUtc.");
    }
}
