using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Quantity).IsRequired();

        builder.HasOne<Cart>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SellerOffer>()
            .WithMany()
            .HasForeignKey(x => x.SellerOfferId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CartId, x.SellerOfferId }).IsUnique();
    }
}
