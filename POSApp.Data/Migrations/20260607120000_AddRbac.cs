using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Create Roles table ──────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "INTEGER", nullable: false)
                                       .Annotation("Sqlite:Autoincrement", true),
                    Name        = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    IsSystemRole = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Roles", x => x.Id));

            migrationBuilder.CreateIndex("IX_Roles_Name", "Roles", "Name", unique: true);

            // ── 2. Create Permissions table ───────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id          = table.Column<int>(type: "INTEGER", nullable: false)
                                       .Annotation("Sqlite:Autoincrement", true),
                    Name        = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Category    = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Permissions", x => x.Id));

            migrationBuilder.CreateIndex("IX_Permissions_Name", "Permissions", "Name", unique: true);

            // ── 3. Create RolePermissions junction table ──────────────────────
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId       = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey("FK_RolePermissions_Permissions_PermissionId",
                        x => x.PermissionId, "Permissions", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_RolePermissions_Roles_RoleId",
                        x => x.RoleId, "Roles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_RolePermissions_PermissionId", "RolePermissions", "PermissionId");

            // ── 4. Seed Roles ─────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO Roles (Id, Name, Description, IsSystemRole, CreatedDate) VALUES
                (1, 'Admin',        'Full access to all features',                 1, '2025-01-01 00:00:00'),
                (2, 'Manager',      'Operations and reports — no admin functions', 1, '2025-01-01 00:00:00'),
                (3, 'Cashier',      'Sales screens only',                          1, '2025-01-01 00:00:00'),
                (4, 'PharmacyUser', 'Pharmacy operations and reports',             1, '2025-01-01 00:00:00');
            ");

            // ── 5. Seed Permissions ───────────────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO Permissions (Id, Name, DisplayName, Category) VALUES
                (1,  'Sale.Access',           'Access Sale Screen',           'Sales'),
                (2,  'WholeSale.Access',       'Access Wholesale Screen',      'Sales'),
                (3,  'SaleReturn.Access',      'Access Sale Return',           'Sales'),
                (4,  'CustomerLedger.Access',  'Access Customer Ledger',       'Sales'),
                (5,  'HoldSale.Access',        'Access Hold Sale',             'Sales'),
                (6,  'Reports.Sales',          'View Sales Reports',           'Reports'),
                (7,  'Reports.Daily',          'View Daily Summary',           'Reports'),
                (8,  'Dashboard.Access',       'Access Dashboard',             'Reports'),
                (9,  'Products.Manage',        'Manage Products',              'Products'),
                (10, 'Categories.Manage',      'Manage Categories',            'Products'),
                (11, 'Expenses.Manage',        'Manage Expenses',              'Operations'),
                (12, 'Shifts.Manage',          'Manage Cash Register/Shifts',  'Operations'),
                (13, 'Purchases.Manage',       'Manage Purchase Orders',       'Operations'),
                (14, 'Suppliers.Manage',       'Manage Suppliers',             'Operations'),
                (15, 'Users.Manage',           'Manage Users & Roles',         'Administration'),
                (16, 'Settings.System',        'System Settings',              'Administration'),
                (17, 'Backup.Access',          'Backup & Restore',             'Administration'),
                (18, 'Pharmacy.Sale',          'Access Pharmacy Sale',         'Pharmacy'),
                (19, 'Pharmacy.Manage',        'Manage Pharmacies',            'Pharmacy'),
                (20, 'Doctors.Manage',         'Manage Doctors',               'Pharmacy');
            ");

            // ── 6. Seed RolePermissions ───────────────────────────────────────
            migrationBuilder.Sql("INSERT INTO RolePermissions (RoleId, PermissionId) SELECT 1, Id FROM Permissions;");
            migrationBuilder.Sql("INSERT INTO RolePermissions (RoleId, PermissionId) SELECT 2, Id FROM Permissions WHERE Id <= 14;");
            migrationBuilder.Sql("INSERT INTO RolePermissions (RoleId, PermissionId) SELECT 3, Id FROM Permissions WHERE Id <= 5;");
            migrationBuilder.Sql("INSERT INTO RolePermissions (RoleId, PermissionId) SELECT 4, Id FROM Permissions WHERE Id IN (4,5,6,7,8,9,10,11,12,13,14,18,19,20);");

            // ── 7. Rebuild Users table (SQLite raw DDL — no DropColumnOperation) ──
            // SQLite doesn't support ALTER TABLE DROP COLUMN via EF without a full snapshot.
            // We do the rebuild manually: create new table → copy+remap → drop old → rename.
            migrationBuilder.Sql(@"
                CREATE TABLE Users_new (
                    Id            INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
                    Username      TEXT    NOT NULL,
                    PasswordHash  TEXT    NOT NULL,
                    CreatedDate   TEXT    NOT NULL,
                    ModifiedDate  TEXT,
                    LastLoginDate TEXT,
                    IsActive      INTEGER NOT NULL,
                    RoleId        INTEGER NOT NULL DEFAULT 3
                        REFERENCES Roles(Id) ON DELETE RESTRICT
                );
            ");

            // Copy data, mapping old Role string → new RoleId in one step
            migrationBuilder.Sql(@"
                INSERT INTO Users_new (Id, Username, PasswordHash, CreatedDate, ModifiedDate, LastLoginDate, IsActive, RoleId)
                SELECT
                    Id, Username, PasswordHash, CreatedDate, ModifiedDate, LastLoginDate, IsActive,
                    CASE Role
                        WHEN 'Admin'        THEN 1
                        WHEN 'Manager'      THEN 2
                        WHEN 'Cashier'      THEN 3
                        WHEN 'PharmacyUser' THEN 4
                        ELSE 3
                    END
                FROM Users;
            ");

            migrationBuilder.Sql("DROP TABLE Users;");
            migrationBuilder.Sql("ALTER TABLE Users_new RENAME TO Users;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);");
            migrationBuilder.Sql("CREATE INDEX IX_Users_RoleId ON Users (RoleId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rebuild Users back to original schema with Role string column
            migrationBuilder.Sql(@"
                CREATE TABLE Users_old (
                    Id            INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
                    Username      TEXT    NOT NULL,
                    PasswordHash  TEXT    NOT NULL,
                    Role          TEXT    NOT NULL DEFAULT 'Cashier',
                    CreatedDate   TEXT    NOT NULL,
                    ModifiedDate  TEXT,
                    LastLoginDate TEXT,
                    IsActive      INTEGER NOT NULL
                );
                INSERT INTO Users_old (Id, Username, PasswordHash, Role, CreatedDate, ModifiedDate, LastLoginDate, IsActive)
                SELECT u.Id, u.Username, u.PasswordHash,
                       COALESCE(r.Name, 'Cashier'),
                       u.CreatedDate, u.ModifiedDate, u.LastLoginDate, u.IsActive
                FROM Users u
                LEFT JOIN Roles r ON r.Id = u.RoleId;
                DROP TABLE Users;
                ALTER TABLE Users_old RENAME TO Users;
                CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);
            ");

            migrationBuilder.DropTable("RolePermissions");
            migrationBuilder.DropTable("Permissions");
            migrationBuilder.DropTable("Roles");
        }
    }
}
