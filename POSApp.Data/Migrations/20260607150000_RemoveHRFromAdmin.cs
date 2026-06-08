using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    public partial class RemoveHRFromAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HR permissions (Employees.Manage=21, Salary.Manage=22) are pharmacy-only.
            migrationBuilder.Sql(
                "DELETE FROM RolePermissions WHERE RoleId = 1 AND PermissionId IN (21, 22);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT OR IGNORE INTO RolePermissions (RoleId, PermissionId) VALUES
                (1, 21), (1, 22);");
        }
    }
}
