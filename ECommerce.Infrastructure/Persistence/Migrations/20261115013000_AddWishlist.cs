using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ECommerceDbContext))]
[Migration("20261115013000_AddWishlist")]
public partial class AddWishlist : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Wishlists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Wishlists", x => x.Id);
                table.ForeignKey(
                    name: "FK_Wishlists_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WishlistItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WishlistId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WishlistItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_WishlistItems_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_WishlistItems_Wishlists_WishlistId",
                    column: x => x.WishlistId,
                    principalTable: "Wishlists",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WishlistItems_ProductId",
            table: "WishlistItems",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_WishlistItems_WishlistId_ProductId",
            table: "WishlistItems",
            columns: new[] { "WishlistId", "ProductId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Wishlists_UserId",
            table: "Wishlists",
            column: "UserId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "WishlistItems");
        migrationBuilder.DropTable(name: "Wishlists");
    }
}
