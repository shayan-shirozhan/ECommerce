using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class WishlistPersistenceTests : PostgresTestBase
{
    [Test]
    public async Task Database_should_enforce_one_wishlist_per_user()
    {
        var userId = Guid.NewGuid();
        var userName = $"wishlist-unique-user-{Guid.NewGuid():N}";
        var email = $"{userName}@example.test";

        // Persist the user and the first wishlist before attempting the duplicate.
        // Using separate DbContext instances ensures this test exercises the database
        // unique index rather than EF Core's one-to-one relationship fixup.
        await using (var setupContext = CreateDbContext())
        {
            setupContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                FirstName = "Wishlist",
                LastName = "Unique"
            });
            setupContext.Wishlists.Add(Wishlist.Create(userId));

            await setupContext.SaveChangesAsync();
        }

        await using var context = CreateDbContext();
        context.Wishlists.Add(Wishlist.Create(userId));

        var exception = Assert.ThrowsAsync<PersistenceConflictException>(
            async () => await context.SaveChangesAsync());

        var dbUpdateException = exception!.InnerException as DbUpdateException;
        var postgresException = dbUpdateException?.InnerException as PostgresException;

        Assert.That(postgresException?.SqlState, Is.EqualTo(PostgresErrorCodes.UniqueViolation));
        Assert.That(postgresException?.ConstraintName, Is.EqualTo("IX_Wishlists_UserId"));
    }

    [Test]
    public async Task Database_should_prevent_duplicate_products_in_the_same_wishlist()
    {
        var userId = Guid.NewGuid();
        var userName = $"wishlist-unique-item-{Guid.NewGuid():N}";
        var email = $"{userName}@example.test";

        await using var context = CreateDbContext();
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Wishlist",
            LastName = "Unique"
        });

        var suffix = Guid.NewGuid().ToString("N");
        var brand = Brand.Create($"Wishlist Unique Brand {suffix}");
        var category = Category.Create($"Wishlist Unique Category {suffix}");
        var product = Product.Create(
            $"Wishlist Unique Product {suffix}",
            "Test product description.",
            brand.Id,
            category.Id);
        var wishlist = Wishlist.Create(userId);
        wishlist.AddProduct(product.Id);

        context.AddRange(brand, category, product, wishlist);
        await context.SaveChangesAsync();

        var exception = Assert.ThrowsAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WishlistItems" ("Id", "WishlistId", "ProductId")
                VALUES ({Guid.NewGuid()}, {wishlist.Id}, {product.Id})
                """));

        Assert.That(exception?.SqlState, Is.EqualTo(PostgresErrorCodes.UniqueViolation));
        Assert.That(
            exception?.ConstraintName,
            Is.EqualTo("IX_WishlistItems_WishlistId_ProductId"));
    }
}
