using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .Must((query, page) =>
                ((long)page - 1L) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset exceeds the supported range.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, GetProductsQuery.MaxPageSize)
            .WithMessage(
                $"Page size must be between 1 and {GetProductsQuery.MaxPageSize}.");
    }
}
