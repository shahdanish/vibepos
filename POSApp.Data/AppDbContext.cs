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

            // Configure SyncLog entity
            modelBuilder.Entity<SyncLog>()
                .HasKey(s => s.Id);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => s.SyncedAt);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => s.EntityType);

            modelBuilder.Entity<SyncLog>()
                .HasIndex(s => new { s.EntityType, s.EntityId });

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
