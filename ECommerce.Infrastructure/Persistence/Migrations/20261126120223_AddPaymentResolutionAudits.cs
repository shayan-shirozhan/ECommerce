using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

public partial class AddPaymentResolutionAudits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentResolutionAudits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                RefId = table.Column<long>(type: "bigint", nullable: true),
                Outcome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ProviderErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PaymentStatusAfter = table.Column<int>(type: "integer", nullable: true),
                OrderStatusAfter = table.Column<int>(type: "integer", nullable: true),
                RestoredItemCount = table.Column<int>(type: "integer", nullable: true),
                RestoredQuantity = table.Column<long>(type: "bigint", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentResolutionAudits", x => x.Id);
                table.ForeignKey(
                    name: "FK_PaymentResolutionAudits_AspNetUsers_AdminUserId",
                    column: x => x.AdminUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentResolutionAudits_Orders_OrderId",
                    column: x => x.OrderId,
                    principalTable: "Orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PaymentResolutionAudits_Payments_PaymentId",
                    column: x => x.PaymentId,
                    principalTable: "Payments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentResolutionAudits_AdminUserId_CreatedAt",
            table: "PaymentResolutionAudits",
            columns: new[] { "AdminUserId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PaymentResolutionAudits_OrderId",
            table: "PaymentResolutionAudits",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentResolutionAudits_PaymentId_CreatedAt",
            table: "PaymentResolutionAudits",
            columns: new[] { "PaymentId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentResolutionAudits");
    }
}
