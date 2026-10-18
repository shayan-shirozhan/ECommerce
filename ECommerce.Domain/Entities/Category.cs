using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Category : AuditableEntity
{
    private Category()
    {
    }

    private Category(string name, Guid? parentCategoryId)
    {
        ValidateName(name);

        Name = name.Trim();
        ParentCategoryId = NormalizeParentId(parentCategoryId);
    }

    public string Name { get; private set; } = null!;

    public Guid? ParentCategoryId { get; private set; }

    public Category? ParentCategory { get; private set; }

    public bool IsDeleted { get; private set; }

    private readonly List<Category> _children = [];

    public IReadOnlyCollection<Category> Children => _children.AsReadOnly();

    public static Category Create(
        string name,
        Guid? parentCategoryId = null) =>
        new(name, parentCategoryId);

    public void UpdateDetails(string name, Guid? parentCategoryId)
    {
        EnsureNotDeleted();
        ValidateName(name);

        var normalizedParentId = NormalizeParentId(parentCategoryId);

        if (normalizedParentId == Id)
            throw new DomainException("A category cannot be its own parent.");

        Name = name.Trim();
        ParentCategoryId = normalizedParentId;
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            throw new DomainException("Category is already deleted.");

        IsDeleted = true;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("A deleted category cannot be modified.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required.");
    }

    private static Guid? NormalizeParentId(Guid? parentCategoryId)
    {
        return parentCategoryId == Guid.Empty
            ? null
            : parentCategoryId;
    }
}
