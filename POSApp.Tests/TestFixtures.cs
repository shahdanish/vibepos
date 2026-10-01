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

    /// <summary>Builds a sale screen over mocked repositories, and a representative cart.</summary>
    internal static class SaleFixtures
    {
        public static SaleViewModel NewSaleScreen(
            Func<string>? nextInvoiceNumber = null,
            List<Sale>? saved = null,
            IFavoriteRepository? favorites = null,
            bool wholesale = false)
        {
            var sales = new Mock<ISaleRepository>();
            sales.Setup(r => r.GetNextInvoiceNumberAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(nextInvoiceNumber ?? (() => "11050"));
            sales.Setup(r => r.AddAsync(It.IsAny<Sale>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Sale s, CancellationToken _) => { saved?.Add(s); return s; });

            var products = new Mock<IProductRepository>();
            products.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());
            var customers = new Mock<ICustomerRepository>();
            customers.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Customer>());

            return wholesale
                ? new WholeSaleViewModel(sales.Object, products.Object, customers.Object, favorites)
                : new SaleViewModel(sales.Object, products.Object, customers.Object, favorites);
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
