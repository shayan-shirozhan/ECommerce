using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed class GetMySellerOffersQueryValidator : AbstractValidator<GetMySellerOffersQuery>
{
    public GetMySellerOffersQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .Must((query, page) =>
                ((long)page - 1L) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, GetMySellerOffersQuery.MaxPageSize)
            .WithMessage(
                $"Page size must be between 1 and {GetMySellerOffersQuery.MaxPageSize}.");

        RuleFor(query => query.Status)
            .Must(status =>
                !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Status is invalid.");
    }
}
