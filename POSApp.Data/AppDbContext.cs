using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POSApp.Core.Entities;

namespace POSApp.Data
{
    public sealed class AppDbContext : DbContext
    {
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }

        // New Features
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<HoldSale> HoldSales { get; set; }
        public DbSet<HoldSaleItem> HoldSaleItems { get; set; }
        public DbSet<CustomerPayment> CustomerPayments { get; set; }

        // New Features
        public DbSet<DailySalesSummary> DailySalesSummaries { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<UserFavorite> UserFavorites { get; set; }
        public DbSet<Pharmacy> Pharmacies { get; set; }
        public DbSet<Doctor> Doctors { get; set; }

        // HR Module
        public DbSet<Employee> Employees { get; set; }
        public DbSet<SalarySlip> SalarySlips { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                options.UseSqlite("Data Source=posapp.db")
                       .AddInterceptors(new SyncLogInterceptor())
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global query filter for soft delete
            modelBuilder.Entity<Product>()
                .HasQueryFilter(p => !p.IsDeleted);

            // Configure Sale entity
            modelBuilder.Entity<Sale>()
                .HasKey(s => s.Id);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Pharmacy)
                .WithMany()
                .HasForeignKey(s => s.PharmacyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Doctor)
                .WithMany()
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Sale>()
                .Property(s => s.TotalBill)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.ReceiveCash)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Sale>()
                .Property(s => s.Balance)
                .HasPrecision(18, 2);

            // Configure SaleItem entity
            modelBuilder.Entity<SaleItem>()
                .HasKey(si => si.Id);

            modelBuilder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.CostPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SaleItem>()
                .Property(si => si.Total)
                .HasPrecision(18, 2);

            // Configure Product entity
            modelBuilder.Entity<Product>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Product>()
                .Property(p => p.CostPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(p => p.WholesalePrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for fast barcode scanning
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode);

            // Configure Category entity
            modelBuilder.Entity<Category>()
                .HasKey(c => c.Id);

            // Configure Role entity
            modelBuilder.Entity<Role>()
                .HasKey(r => r.Id);

            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            // Configure Permission entity
            modelBuilder.Entity<Permission>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Permission>()
                .HasIndex(p => p.Name)
                .IsUnique();

            // Configure RolePermission (composite PK junction table)
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure User entity
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasOne(u => u.UserRole)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ignore computed property
            modelBuilder.Entity<User>()
                .Ignore(u => u.RoleName);

            // Configure ApplicationSetting entity
            modelBuilder.Entity<ApplicationSetting>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<ApplicationSetting>()
                .HasIndex(a => a.Key)
                .IsUnique();

            // Configure Customer entity
            modelBuilder.Entity<Customer>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<Customer>()
                .Property(c => c.PreBalance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Customer>()
                .Property(c => c.CurrentBalance)
                .HasPrecision(18, 2);

            // Customer loyalty fields
            modelBuilder.Entity<Customer>()
                .Property(c => c.TotalPurchases)
                .HasPrecision(18, 2);

            // Configure CustomerPayment
            modelBuilder.Entity<CustomerPayment>()
                .Property(cp => cp.AmountPaid)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CustomerPayment>()
                .HasOne(cp => cp.Customer)
                .WithMany(c => c.Payments)
                .HasForeignKey(cp => cp.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Expense
            modelBuilder.Entity<Expense>()
                .Property(e => e.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ExpenseCategory
            modelBuilder.Entity<ExpenseCategory>()
                .HasKey(c => c.Id);

            // Configure Shift
            modelBuilder.Entity<Shift>()
                .Property(s => s.OpeningBalance)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Shift>()
                .Property(s => s.ExpectedClosingBalance)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Shift>()
                .Property(s => s.ActualClosingBalance)
                .HasPrecision(18, 2);

            // Configure HoldSale and HoldSaleItem
            modelBuilder.Entity<HoldSale>()
                .Property(hs => hs.TotalBill)
                .HasPrecision(18, 2);

            modelBuilder.Entity<HoldSaleItem>()
                .HasOne(hsi => hsi.HoldSale)
                .WithMany(hs => hs.Items)
                .HasForeignKey(hsi => hsi.HoldSaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HoldSaleItem>()
                .Property(hsi => hsi.UnitPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<HoldSaleItem>()
                .Property(hsi => hsi.CostPrice)
                .HasPrecision(18, 2);
            modelBuilder.Entity<HoldSaleItem>()
                .Property(hsi => hsi.Discount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<HoldSaleItem>()
                .Property(hsi => hsi.Total)
                .HasPrecision(18, 2);

            // Configure SyncLog entity
            modelBuilder.Entity<SyncLog>()
                .HasKey(s => s.Id);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => s.SyncedAt);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => s.EntityType);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => new { s.EntityType, s.EntityId });

            // Configure DailySalesSummary
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.OpeningBalance)
                .HasPrecision(18, 2);
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.TotalSales)
                .HasPrecision(18, 2);
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.TotalExpenses)
                .HasPrecision(18, 2);
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.ExpectedClosing)
                .HasPrecision(18, 2);
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.ActualClosing)
                .HasPrecision(18, 2);
            modelBuilder.Entity<DailySalesSummary>()
                .Property(d => d.Variance)
                .HasPrecision(18, 2);

            // Configure PurchaseOrder
            modelBuilder.Entity<PurchaseOrder>()
                .Property(p => p.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(p => p.PurchaseNumber)
                .IsUnique();

            // Configure PurchaseOrderItem
            modelBuilder.Entity<PurchaseOrderItem>()
                .Property(p => p.UnitCost)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseOrderItem>()
                .Property(p => p.Total)
                .HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseOrderItem>()
                .HasOne(p => p.PurchaseOrder)
                .WithMany(p => p.Items)
                .HasForeignKey(p => p.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Supplier
            modelBuilder.Entity<Supplier>()
                .HasKey(s => s.Id);
            modelBuilder.Entity<Supplier>()
                .Property(s => s.CurrentBalance)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Supplier>()
                .HasIndex(s => s.SupplierId)
                .IsUnique();

            // Configure UserFavorite
            modelBuilder.Entity<UserFavorite>()
                .HasIndex(f => new { f.UserId, f.ProductId })
                .IsUnique();
            modelBuilder.Entity<UserFavorite>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<UserFavorite>()
                .HasOne(f => f.Product)
                .WithMany()
                .HasForeignKey(f => f.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Pharmacy
            modelBuilder.Entity<Pharmacy>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Pharmacy>()
                .HasIndex(p => p.LicenseNo)
                .IsUnique()
                .HasFilter("[LicenseNo] IS NOT NULL AND [IsDeleted] = 0");
            modelBuilder.Entity<Pharmacy>()
                .HasQueryFilter(p => !p.IsDeleted);

            // Configure Doctor
            modelBuilder.Entity<Doctor>()
                .HasKey(d => d.Id);
            modelBuilder.Entity<Doctor>()
                .HasQueryFilter(d => !d.IsDeleted);

            // Configure Employee
            modelBuilder.Entity<Employee>()
                .HasKey(e => e.Id);
            modelBuilder.Entity<Employee>()
                .HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Employee>()
                .Property(e => e.BasicSalary)
                .HasPrecision(18, 2);

            // Configure SalarySlip
            modelBuilder.Entity<SalarySlip>()
                .HasKey(s => s.Id);
            modelBuilder.Entity<SalarySlip>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.SalarySlips)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.BasicSalary).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.HouseRentAllowance).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.MedicalAllowance).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.OtherAllowances).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.IncomeTax).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.EobiDeduction).HasPrecision(18, 2);
            modelBuilder.Entity<SalarySlip>()
                .Property(s => s.OtherDeductions).HasPrecision(18, 2);
            // Computed columns — not stored in DB
            modelBuilder.Entity<SalarySlip>()
                .Ignore(s => s.GrossSalary)
                .Ignore(s => s.TotalDeductions)
                .Ignore(s => s.NetSalary)
                .Ignore(s => s.MonthName);

            // ── Seed Data ────────────────────────────────────────────────────────

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Medicine", Description = "Medical products", CreatedDate = new DateTime(2025, 1, 1) },
                new Category { Id = 2, Name = "Stationery", Description = "Office and school supplies", CreatedDate = new DateTime(2025, 1, 1) }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, ProductId = "101124", Barcode = "101124", ProductName = "Glycerin 25gm", CostPrice = 18, UnitPrice = 23, WholesalePrice = 21, Stock = 100, Rack = "A1", CategoryId = 1, CreatedDate = new DateTime(2025, 1, 1) },
                new Product { Id = 2, ProductId = "6939219010101", Barcode = "6939219010101", ProductName = "Glue Stick", CostPrice = 45, UnitPrice = 60, WholesalePrice = 55, Stock = 46, Rack = "B2", CategoryId = 2, CreatedDate = new DateTime(2025, 1, 1) }
            );

            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, CustomerId = "CASH", Name = "Cash", PreBalance = 0, CreatedDate = new DateTime(2025, 1, 1) }
            );

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin", Description = "Full access to all features", IsSystemRole = true, CreatedDate = new DateTime(2025, 1, 1) },
                new Role { Id = 2, Name = "Manager", Description = "Operations and reports — no admin functions", IsSystemRole = true, CreatedDate = new DateTime(2025, 1, 1) },
                new Role { Id = 3, Name = "Cashier", Description = "Sales screens only", IsSystemRole = true, CreatedDate = new DateTime(2025, 1, 1) },
                new Role { Id = 4, Name = "PharmacyUser", Description = "Pharmacy operations and reports", IsSystemRole = true, CreatedDate = new DateTime(2025, 1, 1) }
            );

            // Seed Permissions (20 permissions grouped by category)
            modelBuilder.Entity<Permission>().HasData(
                // Sales (1-5)
                new Permission { Id = 1,  Name = "Sale.Access",            DisplayName = "Access Sale Screen",          Category = "Sales" },
                new Permission { Id = 2,  Name = "WholeSale.Access",       DisplayName = "Access Wholesale Screen",     Category = "Sales" },
                new Permission { Id = 3,  Name = "SaleReturn.Access",      DisplayName = "Access Sale Return",          Category = "Sales" },
                new Permission { Id = 4,  Name = "CustomerLedger.Access",  DisplayName = "Access Customer Ledger",      Category = "Sales" },
                new Permission { Id = 5,  Name = "HoldSale.Access",        DisplayName = "Access Hold Sale",            Category = "Sales" },
                // Reports (6-8)
                new Permission { Id = 6,  Name = "Reports.Sales",          DisplayName = "View Sales Reports",          Category = "Reports" },
                new Permission { Id = 7,  Name = "Reports.Daily",          DisplayName = "View Daily Summary",          Category = "Reports" },
                new Permission { Id = 8,  Name = "Dashboard.Access",       DisplayName = "Access Dashboard",            Category = "Reports" },
                // Products (9-10)
                new Permission { Id = 9,  Name = "Products.Manage",        DisplayName = "Manage Products",             Category = "Products" },
                new Permission { Id = 10, Name = "Categories.Manage",      DisplayName = "Manage Categories",           Category = "Products" },
                // Operations (11-14)
                new Permission { Id = 11, Name = "Expenses.Manage",        DisplayName = "Manage Expenses",             Category = "Operations" },
                new Permission { Id = 12, Name = "Shifts.Manage",          DisplayName = "Manage Cash Register/Shifts", Category = "Operations" },
                new Permission { Id = 13, Name = "Purchases.Manage",       DisplayName = "Manage Purchase Orders",      Category = "Operations" },
                new Permission { Id = 14, Name = "Suppliers.Manage",       DisplayName = "Manage Suppliers",            Category = "Operations" },
                // Administration (15-17)
                new Permission { Id = 15, Name = "Users.Manage",           DisplayName = "Manage Users & Roles",        Category = "Administration" },
                new Permission { Id = 16, Name = "Settings.System",        DisplayName = "System Settings",             Category = "Administration" },
                new Permission { Id = 17, Name = "Backup.Access",          DisplayName = "Backup & Restore",            Category = "Administration" },
                // Pharmacy (18-20)
                new Permission { Id = 18, Name = "Pharmacy.Sale",          DisplayName = "Access Pharmacy Sale",        Category = "Pharmacy" },
                new Permission { Id = 19, Name = "Pharmacy.Manage",        DisplayName = "Manage Pharmacies",           Category = "Pharmacy" },
                new Permission { Id = 20, Name = "Doctors.Manage",         DisplayName = "Manage Doctors",              Category = "Pharmacy" },
                // HR (21-22)
                new Permission { Id = 21, Name = "Employees.Manage",       DisplayName = "Manage Employees",            Category = "HR" },
                new Permission { Id = 22, Name = "Salary.Manage",          DisplayName = "Manage Salary Slips",         Category = "HR" }
            );

            // Seed RolePermissions
            // Admin (1): 1-20 (no HR — employees/salary are pharmacy-only)
            var adminPerms = Enumerable.Range(1, 20).Select(i => new RolePermission { RoleId = 1, PermissionId = i });
            // Manager (2): 1-14 (no administration, no pharmacy)
            var managerPerms = Enumerable.Range(1, 14).Select(i => new RolePermission { RoleId = 2, PermissionId = i });
            // Cashier (3): 1-5 (sales only)
            var cashierPerms = Enumerable.Range(1, 5).Select(i => new RolePermission { RoleId = 3, PermissionId = i });
            // PharmacyUser (4): customer ledger, hold sale, reports, products, operations, pharmacy, HR
            var pharmacyPerms = new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 18, 19, 20, 21, 22 }
                .Select(i => new RolePermission { RoleId = 4, PermissionId = i });

            modelBuilder.Entity<RolePermission>().HasData(
                adminPerms.Concat(managerPerms).Concat(cashierPerms).Concat(pharmacyPerms).ToArray()
            );

            // Seed default users (RoleId replaces the old Role string)
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin",   PasswordHash = "admin123",   RoleId = 1, CreatedDate = new DateTime(2025, 1, 1), IsActive = true },
                new User { Id = 2, Username = "cashier", PasswordHash = "cashier123", RoleId = 3, CreatedDate = new DateTime(2025, 1, 1), IsActive = true },
                new User { Id = 3, Username = "ali",     PasswordHash = "ali443",     RoleId = 1, CreatedDate = new DateTime(2025, 1, 1), IsActive = true },
                new User { Id = 4, Username = "alico",   PasswordHash = "1",          RoleId = 4, CreatedDate = new DateTime(2025, 1, 1), IsActive = true }
            );

            // Seed default application settings
            modelBuilder.Entity<ApplicationSetting>().HasData(
                new ApplicationSetting
                {
                    Id = 1,
                    Key = "DefaultProfitMarginPercentage",
                    Value = "200",
                    Description = "Default profit margin percentage for auto-calculating selling price from cost",
                    CreatedDate = new DateTime(2025, 1, 1)
                },
                new ApplicationSetting
                {
                    Id = 2,
                    Key = "LowStockAlertEnabled",
                    Value = "true",
                    Description = "Enable/disable low stock alerts",
                    CreatedDate = new DateTime(2025, 1, 1)
                }
            );
        }
    }
}
