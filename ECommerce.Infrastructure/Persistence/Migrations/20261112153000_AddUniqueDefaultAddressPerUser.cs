using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ECommerceDbContext))]
[Migration("20261112153000_AddUniqueDefaultAddressPerUser")]
public partial class AddUniqueDefaultAddressPerUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH ranked_addresses AS
            (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "UserId"
                           ORDER BY "CreatedAt" DESC, "Id") AS row_number
                FROM "Addresses"
                WHERE "IsDefault" = TRUE
            )
            UPDATE "Addresses" AS address
            SET "IsDefault" = FALSE,
                "UpdatedAt" = NOW()
            FROM ranked_addresses
            WHERE address."Id" = ranked_addresses."Id"
              AND ranked_addresses.row_number > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Addresses_UserId_Default",
            table: "Addresses",
            column: "UserId",
            unique: true,
            filter: "\"IsDefault\" = TRUE");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Addresses_UserId_Default",
            table: "Addresses");
    }
}
