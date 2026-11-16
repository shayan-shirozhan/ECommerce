using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRetrySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Authority",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefId",
                table: "Payments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Payments",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Authority",
                table: "Payments",
                column: "Authority",
                unique: true,
                filter: "\"Authority\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefId",
                table: "Payments",
                column: "RefId",
                unique: true,
                filter: "\"RefId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_Authority",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Active",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Authority",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Payments");
        }
    }
}
