using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentManualReviewTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstVerificationAttemptedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastVerificationErrorCode",
                table: "Payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualReviewRequiredAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationAttemptCount",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve a conservative retry history for payments that were already
            // awaiting verification when this migration is deployed.
            migrationBuilder.Sql("""
                UPDATE "Payments"
                SET "FirstVerificationAttemptedAt" = "LastVerificationAttemptedAt",
                    "VerificationAttemptCount" = 1
                WHERE "LastVerificationAttemptedAt" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" IN (1, 2, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FirstVerificationAttemptedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastVerificationErrorCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ManualReviewRequiredAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerificationAttemptCount",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" IN (1, 2)");
        }
    }
}
