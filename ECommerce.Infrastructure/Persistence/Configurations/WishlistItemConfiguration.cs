using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.HasKey(item => item.Id);

        builder.HasOne<Wishlist>()
            .WithMany(wishlist => wishlist.Items)
            .HasForeignKey(item => item.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.WishlistId, item.ProductId })
            .IsUnique();

        builder.HasIndex(item => item.ProductId);
    }
}
