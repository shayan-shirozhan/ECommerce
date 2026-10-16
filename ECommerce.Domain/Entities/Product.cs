using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Product : AuditableEntity
{
    //ctor's
    private Product()
    {

    }

    private Product(string name, string description, Guid brandId, Guid categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Product description is required.");

        if (brandId == Guid.Empty)
            throw new DomainException("Brand ID is required.");

        if (categoryId == Guid.Empty)
            throw new DomainException("Category ID is required.");

        Name = name.Trim();
        Description = description.Trim();
        BrandId = brandId;
        CategoryId = categoryId;
        Status = ProductStatus.Draft;
    }

    //prop's
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    public Guid BrandId { get; private set; }
    public Brand? Brand { get; private set; } = null!;

    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public ProductStatus Status { get; private set; } = ProductStatus.Draft;

    private readonly List<ProductVariant> _variants = [];
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    private readonly List<ProductImage> _images = [];
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();


    //method's
    public static Product Create(string name, string description, Guid brandId, Guid categoryId) =>
        new(name, description, brandId, categoryId);

    public void Activate()
    {
        if (Status == ProductStatus.Active)
            throw new DomainException("Product is already active.");

        Status = ProductStatus.Active;
    }

    public void Deactivate()
    {
        if (Status == ProductStatus.Inactive)
            throw new DomainException("Product is already inactive.");

        Status = ProductStatus.Inactive;
    }
}
