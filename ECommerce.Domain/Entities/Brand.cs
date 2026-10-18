using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Brand : AuditableEntity
{
    private Brand()
    {
    }

    private Brand(string name)
    {
        ValidateName(name);
        Name = name.Trim();
    }

    public string Name { get; private set; } = null!;

    public bool IsDeleted { get; private set; }

    public static Brand Create(string name) => new(name);

    public void UpdateName(string name)
    {
        EnsureNotDeleted();
        ValidateName(name);

        Name = name.Trim();
    }

    public void SoftDelete()
    {
        if (IsDeleted)
            throw new DomainException("Brand is already deleted.");

        IsDeleted = true;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("A deleted brand cannot be modified.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Brand name is required.");
    }
}
