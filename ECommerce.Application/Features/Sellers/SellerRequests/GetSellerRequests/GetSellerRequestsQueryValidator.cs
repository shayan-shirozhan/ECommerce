using FluentValidation;

namespace ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed class GetSellerRequestsQueryValidator : AbstractValidator<GetSellerRequestsQuery>
{
    public GetSellerRequestsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .Must((query, page) =>
                ((long)page - 1L) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, GetSellerRequestsQuery.MaxPageSize)
            .WithMessage(
                $"Page size must be between 1 and {GetSellerRequestsQuery.MaxPageSize}.");

        RuleFor(query => query.Status)
            .Must(status =>
                !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Status is invalid.");
    }
}
