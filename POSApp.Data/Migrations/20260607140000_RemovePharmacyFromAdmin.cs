using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    public partial class RemovePharmacyFromAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Admin role (Id=1) should not see pharmacy screens.
            // Pharmacy.Sale=18, Pharmacy.Manage=19, Doctors.Manage=20
            migrationBuilder.Sql(
                "DELETE FROM RolePermissions WHERE RoleId = 1 AND PermissionId IN (18, 19, 20);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO RolePermissions (RoleId, PermissionId) VALUES
                (1, 18), (1, 19), (1, 20);");
        }
    }
}
