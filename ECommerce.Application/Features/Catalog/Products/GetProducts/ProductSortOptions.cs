namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public static class ProductSortOptions
{
    public const string Newest = "newest";
    public const string PriceAscending = "price-asc";
    public const string PriceDescending = "price-desc";

    public static bool IsValid(string? value)
    {
        return string.Equals(
                   value,
                   Newest,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   value,
                   PriceAscending,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   value,
                   PriceDescending,
                   StringComparison.OrdinalIgnoreCase);
    }
}
