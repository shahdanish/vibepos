using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <summary>
    /// US pharmacy front store: age, PSE and FSA/HSA columns on Products, the ID-check age on
    /// Sales, and the new PseLogEntries table. Additive only; the UpdateData EF scaffolded for the
    /// seed products (new columns at their defaults) was removed so no existing row is written.
    /// </summary>
    public partial class AddFrontStorePharmacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCheckedAge",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFsaEligible",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPse",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAge",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PseBaseMgPerPack",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PseLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PurchaserName = table.Column<string>(type: "TEXT", nullable: false),
                    PurchaserAddress = table.Column<string>(type: "TEXT", nullable: true),
                    IdType = table.Column<string>(type: "TEXT", nullable: false),
                    IdNumber = table.Column<string>(type: "TEXT", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", nullable: false),
                    Packages = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BaseMg = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    RecordedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PseLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PseLogEntries_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });


            migrationBuilder.CreateIndex(
                name: "IX_PseLogEntries_IdNumber_PurchaseDate",
                table: "PseLogEntries",
                columns: new[] { "IdNumber", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PseLogEntries_SaleId",
                table: "PseLogEntries",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PseLogEntries");

            migrationBuilder.DropColumn(
                name: "IdCheckedAge",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsFsaEligible",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPse",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MinimumAge",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PseBaseMgPerPack",
                table: "Products");
        }
    }
}
