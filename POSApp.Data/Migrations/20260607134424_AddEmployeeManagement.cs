using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Employees table ───────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeCode = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FatherName = table.Column<string>(type: "TEXT", nullable: true),
                    Cnic = table.Column<string>(type: "TEXT", nullable: true),
                    CellNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Designation = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: true),
                    JoiningDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            // ── SalarySlips table ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "SalarySlips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SlipNumber = table.Column<string>(type: "TEXT", nullable: false),
                    EmployeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HouseRentAllowance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MedicalAllowance = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OtherAllowances = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IncomeTax = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EobiDeduction = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedByUsername = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalarySlips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalarySlips_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalarySlips_EmployeeId",
                table: "SalarySlips",
                column: "EmployeeId");

            // ── New HR permissions ────────────────────────────────────────────
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "DisplayName", "Name" },
                values: new object[,]
                {
                    { 21, "HR", "Manage Employees", "Employees.Manage" },
                    { 22, "HR", "Manage Salary Slips", "Salary.Manage" }
                });

            // Admin (RoleId=1) gets both new permissions
            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 21, 1 },
                    { 22, 1 },
                    { 21, 4 },
                    { 22, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 21, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 22, 1 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 21, 4 });

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 22, 4 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DropTable(name: "SalarySlips");
            migrationBuilder.DropTable(name: "Employees");
        }
    }
}
