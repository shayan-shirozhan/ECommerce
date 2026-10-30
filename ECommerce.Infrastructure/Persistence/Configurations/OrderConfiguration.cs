using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(order => order.RowVersion).IsRowVersion();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ShippingRecipientName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.ShippingPhoneNumber).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ShippingProvince).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ShippingCity).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ShippingPostalCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ShippingAddressLine).IsRequired().HasMaxLength(500);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
