using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class PaymentResolutionAuditConfiguration : IEntityTypeConfiguration<PaymentResolutionAudit>
{
    public void Configure(EntityTypeBuilder<PaymentResolutionAudit> builder)
    {
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Action).IsRequired();
        builder.Property(audit => audit.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(audit => audit.Outcome).HasMaxLength(100);
        builder.Property(audit => audit.ProviderErrorCode).HasMaxLength(50);
        builder.Property(audit => audit.PaymentStatusAfter);
        builder.Property(audit => audit.OrderStatusAfter);
        builder.Property(audit => audit.CompletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(audit => audit.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(audit => audit.UpdatedAt).HasColumnType("timestamp with time zone");

        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(audit => audit.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(audit => audit.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ECommerce.Infrastructure.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(audit => audit.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(audit => new { audit.PaymentId, audit.CreatedAt });
        builder.HasIndex(audit => new { audit.AdminUserId, audit.CreatedAt });
    }
}
