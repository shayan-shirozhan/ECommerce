using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> builder)
    {
        builder.HasKey(wishlist => wishlist.Id);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Wishlist>(wishlist => wishlist.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(wishlist => wishlist.UserId)
            .IsUnique();
    }
}
