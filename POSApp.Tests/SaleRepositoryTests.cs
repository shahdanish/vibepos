using POSApp.Core.Entities;
using POSApp.Infrastructure.Repositories;

namespace POSApp.Tests
{
    [Collection(GlobalStateCollection.Name)]
    public sealed class SaleRepositoryTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static Sale NewSale(string invoice, string type = "Sale", params SaleItem[] items)
        {
            var sale = new Sale { InvoiceNumber = invoice, SaleType = type, SaleDate = new DateTime(2026, 10, 14, 12, 0, 0) };
            sale.SaleItems.AddRange(items);
            return sale;
        }

        [Fact]
        public async Task NextInvoiceNumber_OnAnEmptyDatabase_Is11016()
        {
            using var db = _dir.NewMigratedContext();
            Assert.Equal("11016", await new SaleRepository(db).GetNextInvoiceNumberAsync());
        }

        [Fact]
        public async Task NextInvoiceNumber_IsNotResetByAReturn()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new SaleRepository(db);
            await repo.AddAsync(NewSale("11049"));
            await repo.AddAsync(NewSale("11050"));
            await repo.AddAsync(NewSale("R-11051", "Return")); // the newest row is a return

            // The old code parsed only the newest row, failed on "R-…" and restarted at 11016.
            // The return took 11051, so the next number is 11052.
            Assert.Equal("11052", await repo.GetNextInvoiceNumberAsync());
        }

        [Fact]
        public async Task ConsecutiveReturns_GetDifferentNumbers()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new SaleRepository(db);
            await repo.AddAsync(NewSale("11055"));

            var first = "R-" + await repo.GetNextInvoiceNumberAsync();
            await repo.AddAsync(NewSale(first, "Return"));
            var second = "R-" + await repo.GetNextInvoiceNumberAsync();
            await repo.AddAsync(NewSale(second, "Return"));

            Assert.Equal("R-11056", first);
            Assert.Equal("R-11057", second);
            Assert.Equal("11058", await repo.GetNextInvoiceNumberAsync());
        }

        [Fact]
        public async Task NextInvoiceNumber_ComparesNumbersByValue()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new SaleRepository(db);
            await repo.AddAsync(NewSale("11050"));
            await repo.AddAsync(NewSale("9999"));    // newer row, smaller number
            await repo.AddAsync(NewSale("ABC-7"));   // not a plain number: ignored

            Assert.Equal("11051", await repo.GetNextInvoiceNumberAsync());
        }

        [Fact]
        public async Task SalesByCategory_FindsTheCategoryByProductCode()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new SaleRepository(db);
            // Seeded products: "101124" (Medicine) and "6939219010101" (Stationery).
            await repo.AddAsync(NewSale("11050", "Sale",
                new SaleItem { ProductId = "101124", ProductName = "Glycerin 25gm", Quantity = 2, UnitPrice = 23, CostPrice = 18, Total = 46 },
                new SaleItem { ProductId = "6939219010101", ProductName = "Glue Stick", Quantity = 1, UnitPrice = 60, CostPrice = 45, Total = 60 },
                new SaleItem { ProductId = "GONE", ProductName = "Old item", Quantity = 1, UnitPrice = 5, CostPrice = 4, Total = 5 }));

            var day = new DateTime(2026, 10, 14);
            var byCategory = (await repo.GetSalesByCategoryAsync(day, day)).ToDictionary(c => c.CategoryName);

            Assert.Equal(46m, byCategory["Medicine"].TotalSales);
            Assert.Equal(10m, byCategory["Medicine"].TotalProfit);
            Assert.Equal(60m, byCategory["Stationery"].TotalSales);
            Assert.Equal(5m, byCategory["Uncategorized"].TotalSales);
        }
    }
}
