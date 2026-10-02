using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <summary>
    /// US checkout: sales tax and tenders. Additive only — new nullable/defaulted columns and two
    /// new tables. No foreign key is added to an existing table (SQLite would rebuild it), and the
    /// UpdateData EF scaffolded for the seed rows was removed: it only re-set the new columns to
    /// their defaults, and this migration must not write to existing rows.
    /// </summary>
    public partial class AddSalesTaxAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxExemptNumber",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxTotal",
                table: "Sales",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "SaleItems",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "SaleItems",
                type: "TEXT",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TaxCategoryId",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxExempt",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TaxExemptNumber",
                table: "Customers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Tendered = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CardBrand = table.Column<string>(type: "TEXT", nullable: true),
                    CardLast4 = table.Column<string>(type: "TEXT", nullable: true),
                    Reference = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalePayments_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RatePercent = table.Column<decimal>(type: "TEXT", precision: 9, scale: 4, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCategories", x => x.Id);
                });



            migrationBuilder.CreateIndex(
                name: "IX_Products_TaxCategoryId",
                table: "Products",
                column: "TaxCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalePayments_SaleId",
                table: "SalePayments",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalePayments");

            migrationBuilder.DropTable(
                name: "TaxCategories");

            migrationBuilder.DropIndex(
                name: "IX_Products_TaxCategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxExemptNumber",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxTotal",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "TaxCategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsTaxExempt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "TaxExemptNumber",
                table: "Customers");
        }
    }
}
