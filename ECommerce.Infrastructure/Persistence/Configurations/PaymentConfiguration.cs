using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Authority).HasMaxLength(100);
        builder.Property(x => x.LastGatewayRequestStartedAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastVerificationAttemptedAt)
            .HasColumnType("timestamp with time zone");
        builder.Property(x => x.TransactionId).HasMaxLength(200);
        builder.Property(x => x.GatewayReference).HasMaxLength(200);

        builder.HasOne<Order>()
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // One in-flight or successful payment per order; failed/cancelled attempts
        // remain in history and do not prevent a new attempt.
        builder.HasIndex(x => x.OrderId);

        builder.HasIndex(x => x.OrderId, "IX_Payments_OrderId_Active")
            .IsUnique()
            .HasDatabaseName("IX_Payments_OrderId_Active")
            .HasFilter("\"Status\" IN (1, 2)");

        builder.HasIndex(x => x.Authority)
            .IsUnique()
            .HasDatabaseName("IX_Payments_Authority")
            .HasFilter("\"Authority\" IS NOT NULL");

        builder.HasIndex(x => x.RefId)
            .IsUnique()
            .HasDatabaseName("IX_Payments_RefId")
            .HasFilter("\"RefId\" IS NOT NULL");

        builder.HasIndex(x => x.TransactionId).IsUnique();
    }
}
