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
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }
        
        // New Features
        public DbSet<Expense> Expenses { get; set; }
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

            // Configure User entity
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

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

            // Seed sample data
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

            // Seed default users
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "admin123", // In production, use proper hashing like BCrypt
                    Role = "Admin",
                    CreatedDate = new DateTime(2025, 1, 1),
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    Username = "cashier",
                    PasswordHash = "cashier123",
                    Role = "Cashier",
                    CreatedDate = new DateTime(2025, 1, 1),
                    IsActive = true
                },
                new User
                {
                    Id = 3,
                    Username = "ali",
                    PasswordHash = "ali443",
                    Role = "Admin",
                    CreatedDate = new DateTime(2025, 1, 1),
                    IsActive = true
                }
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
