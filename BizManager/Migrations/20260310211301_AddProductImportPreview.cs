using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizManager.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImportPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "products_import_preview",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    product_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    mold_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    barcode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    product_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    collection = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    price = table.Column<decimal>(type: "TEXT", nullable: false),
                    stock = table.Column<int>(type: "INTEGER", nullable: false),
                    is_header = table.Column<bool>(type: "INTEGER", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    catalog_id = table.Column<int>(type: "INTEGER", nullable: false),
                    dealer_id = table.Column<int>(type: "INTEGER", nullable: false),
                    price_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products_import_preview", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "products_import_preview");
        }
    }
}
