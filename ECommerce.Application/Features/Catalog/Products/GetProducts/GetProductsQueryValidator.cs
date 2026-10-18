using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryValidator
    : AbstractValidator<GetProductsQuery>
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

        RuleFor(query => query.CategoryId)
            .Must(categoryId =>
                !categoryId.HasValue || categoryId.Value != Guid.Empty)
            .WithMessage("Category ID must not be empty.");

        RuleFor(query => query.BrandId)
            .Must(brandId =>
                !brandId.HasValue || brandId.Value != Guid.Empty)
            .WithMessage("Brand ID must not be empty.");

        RuleFor(query => query.MinPrice)
            .Must(price => !price.HasValue || price.Value >= 0m)
            .WithMessage("Minimum price must be greater than or equal to zero.");

        RuleFor(query => query.MaxPrice)
            .Must(price => !price.HasValue || price.Value >= 0m)
            .WithMessage("Maximum price must be greater than or equal to zero.");

        RuleFor(query => query.MinPrice)
            .Must((query, minPrice) =>
                !minPrice.HasValue
                || !query.MaxPrice.HasValue
                || minPrice.Value <= query.MaxPrice.Value)
            .WithMessage("Minimum price must not exceed maximum price.");

        RuleFor(query => query.Search)
            .MaximumLength(100)
            .WithMessage("Search text cannot exceed 100 characters.");

        RuleFor(query => query.SortBy)
            .Must(ProductSortOptions.IsValid)
            .WithMessage(
                "SortBy must be one of: newest, price-asc, price-desc.");
    }
}
