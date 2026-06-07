using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacySaleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DoctorId",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Sales",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DoctorId",
                table: "Sales",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PharmacyId",
                table: "Sales",
                column: "PharmacyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Doctors_DoctorId",
                table: "Sales",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Pharmacies_PharmacyId",
                table: "Sales",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Doctors_DoctorId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Pharmacies_PharmacyId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_DoctorId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PharmacyId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Sales");
        }
    }
}
