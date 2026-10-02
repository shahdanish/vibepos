using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Data;
using POSApp.UI.ViewModels;

namespace POSApp.Tests
{
    /// <summary>A clock that only moves when a test moves it.</summary>
    internal sealed class FixedClock : TimeProvider
    {
        public FixedClock(DateTime now) => Now = now;

        /// <summary>Local wall-clock time reported to the app.</summary>
        public DateTime Now { get; set; }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), TimeSpan.Zero);
    }

    /// <summary>
    /// Points <see cref="AppPaths"/> at a fresh, empty data folder for one test and puts the
    /// run-wide temporary folder back afterwards. Use only inside the global-state collection.
    /// </summary>
    internal sealed class TempDataDirectory : IDisposable
    {
        private readonly string? _previous = Environment.GetEnvironmentVariable("POSAPP_DATA_DIR");

        public TempDataDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "posapp-test-db-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", Path);
            AppPaths.ResetForTests();
            RegionSettingsStore.Reload();
        }

        public string Path { get; }

        /// <summary>A context on this folder's database, fully migrated.</summary>
        public AppDbContext NewMigratedContext()
        {
            var db = new AppDbContext();
            db.Database.Migrate();
            return db;
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("POSAPP_DATA_DIR", _previous);
            AppPaths.ResetForTests();
            RegionSettingsStore.Reload();
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(Path, recursive: true); } catch { /* temp folder */ }
        }
    }

    /// <summary>
    /// Rows written the way an older release stored them. Plain SQL with only the columns that
    /// existed then: today's model has more, so it can't insert into an old-schema database.
    /// </summary>
    internal static class LegacyRows
    {
        /// <summary>One 46.00 sale of two "Glycerin 25gm", as the previous release saved it.</summary>
        public static void InsertPreviousReleaseSale(AppDbContext db, string customerName = "Cash")
        {
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Sales (InvoiceNumber, SaleDate, SaleType, PaymentType, CustomerName, PreBalance, " +
                "DiscountOnProducts, DiscountOnBill, TotalBill, ReceiveCash, Balance, AutoPrinted, CreatedDate) " +
                "VALUES ('11050', '2026-09-01 10:00:00', 'Sale', 'Cash', {0}, '0.0', '0.0', '0.0', '46.0', '0.0', '0.0', 0, '2026-09-01 10:00:00')",
                customerName);
            db.Database.ExecuteSqlRaw(
                "INSERT INTO SaleItems (SaleId, ProductId, ProductName, Quantity, Bonus, CostPrice, UnitPrice, DiscountPercent, DiscountType, Total) " +
                "VALUES ((SELECT MAX(Id) FROM Sales), '101124', 'Glycerin 25gm', '2.0', 0, '0.0', '23.0', '0.0', '%', '46.0')");
        }
    }

    /// <summary>Builds a sale screen over mocked repositories, and a representative cart.</summary>
    internal static class SaleFixtures
    {
        public static SaleViewModel NewSaleScreen(
            Func<string>? nextInvoiceNumber = null,
            List<Sale>? saved = null,
            IFavoriteRepository? favorites = null,
            bool wholesale = false,
            ITaxRepository? tax = null,
            IEnumerable<Product>? catalogue = null,
            List<Customer>? updatedCustomers = null)
        {
            var sales = new Mock<ISaleRepository>();
            sales.Setup(r => r.GetNextInvoiceNumberAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(nextInvoiceNumber ?? (() => "11050"));
            sales.Setup(r => r.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Sale s, CancellationToken _) => { saved?.Add(s); return s; });

            var products = new Mock<IProductRepository>();
            products.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync((catalogue ?? Enumerable.Empty<Product>()).ToList());
            var customers = new Mock<ICustomerRepository>();
            customers.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Customer>());
            customers.Setup(r => r.UpdateAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                     .Callback((Customer c, CancellationToken _) => updatedCustomers?.Add(c))
                     .Returns(Task.CompletedTask);

            return wholesale
                ? new WholeSaleViewModel(sales.Object, products.Object, customers.Object, favorites, tax)
                : new SaleViewModel(sales.Object, products.Object, customers.Object, favorites, tax);
        }

        /// <summary>A cart that exercises %, flat-amount and no discount, a fractional price and bill discounts.</summary>
        public static void FillCart(SaleViewModel vm)
        {
            vm.InvoiceNumber = "11050";
            vm.SaleDate = new DateTime(2026, 10, 14, 14, 35, 0);
            vm.CustomerName = "Cash";
            vm.SaleItems.Add(new SaleItemViewModel { ProductId = "101124", ProductName = "Glycerin 25gm", Quantity = 2, CostPrice = 18, UnitPrice = 23 });
            vm.SaleItems.Add(new SaleItemViewModel { ProductId = "2002", ProductName = "Cooking Oil 1L", Quantity = 3, CostPrice = 400, UnitPrice = 1250.5m, DiscountType = "%", DiscountPercent = 10 });
            vm.SaleItems.Add(new SaleItemViewModel { ProductId = "3003", ProductName = "Sugar 5kg", Quantity = 1, CostPrice = 300, UnitPrice = 350, DiscountType = "AMT", DiscountPercent = 25 });
            vm.DiscountOnProducts = 5;
            vm.DiscountOnBill = 10.25m;
            vm.ReceiveCash = 4000;
        }
    }
}
