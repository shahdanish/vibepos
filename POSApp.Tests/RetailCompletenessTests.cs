using System.IO;
using Microsoft.EntityFrameworkCore;
using POSApp.Core.Entities;
using POSApp.Core.Services;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.Services;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;

namespace POSApp.Tests
{
    /// <summary>CSV export: Excel-ready and safe to open.</summary>
    public sealed class CsvTests
    {
        [Fact]
        public void FieldsWithCommasOrQuotes_AreQuoted()
        {
            Assert.Equal("a,\"b,c\",\"say \"\"hi\"\"\"", Csv.Line(new[] { "a", "b,c", "say \"hi\"" }));
        }

        [Theory]
        [InlineData("=SUM(A1)", "'=SUM(A1)")]
        [InlineData("+1 555", "'+1 555")]
        [InlineData("@cmd", "'@cmd")]
        [InlineData("-2", "'-2")]
        [InlineData("Linda", "Linda")]
        public void FreeTextThatLooksLikeAFormula_IsDefused(string input, string expected)
        {
            Assert.Equal(expected, Csv.Text(input));
        }

        [Fact]
        public void Numbers_AreInvariant_AndRefundsStayNegative()
        {
            Assert.Equal("-10.50", Csv.Money(-10.5m));
            Assert.Equal("1234.00", Csv.Money(1234m));
        }

        [Fact]
        public void SalesExport_ListsTaxAndEachTender()
        {
            var sale = new Sale { SaleDate = new DateTime(2026, 10, 2, 9, 5, 0), InvoiceNumber = "11016", SaleType = "Sale", CustomerName = "Cash", PaymentType = "Split", TotalBill = 22.16m, TaxTotal = 1.69m, ReceiveCash = 22.16m };
            sale.SaleItems.Add(new SaleItem { Quantity = 3 });
            sale.Payments.Add(new SalePayment { Method = "Cash", Amount = 10m });
            sale.Payments.Add(new SalePayment { Method = "Card", Amount = 12.16m });

            var rows = SalesReportViewModel.SalesCsvRows(new[] { sale }).Select(r => r.ToList()).ToList();
            Assert.Equal("Tax", rows[0][7]);
            Assert.Equal(new[] { "2026-10-02 09:05", "11016", "Sale", "Cash", "Cash 10.00 + Card 12.16", "3", "20.47", "1.69", "22.16", "22.16", "0.00" }, rows[1]);
        }
    }

    /// <summary>End-of-day X/Z figures.</summary>
    public sealed class ShiftReportTests
    {
        private static Sale Sale(int id, decimal total, string payment = "Cash", decimal receive = -1, string type = "Sale", decimal tax = 0)
            => new() { Id = id, SaleType = type, TotalBill = total, PaymentType = payment, ReceiveCash = receive < 0 ? total : receive, TaxTotal = tax };

        [Fact]
        public void XReport_SumsSalesTendersAndExpectedCash()
        {
            var shift = new Shift { OpenedAt = new DateTime(2026, 10, 2, 8, 0, 0), OpeningBalance = 100m };

            var legacy = Sale(1, 50m, receive: 60m);                     // no tenders: counted as before
            var split = Sale(2, 45.95m, "Split", 50m, tax: 3.50m);
            split.Payments.Add(new SalePayment { Method = "Cash", Amount = 20m, Tendered = 24.05m });
            split.Payments.Add(new SalePayment { Method = "Card", Amount = 25.95m, Tendered = 25.95m });
            var refund = Sale(3, -10m, "Cash", -10m, "Return", tax: -0.75m);
            refund.Payments.Add(new SalePayment { Method = "Cash", Amount = -10m, Tendered = -10m });

            var r = ShiftReportBuilder.Build(shift, new[] { legacy, split, refund }, expenses: 5m, to: new DateTime(2026, 10, 2, 17, 0, 0));

            Assert.False(r.IsZ);
            Assert.Equal(2, r.SalesCount);
            Assert.Equal(95.95m, r.GrossSales);
            Assert.Equal(1, r.ReturnsCount);
            Assert.Equal(10m, r.Returns);
            Assert.Equal(85.95m, r.NetSales);
            Assert.Equal(2.75m, r.TaxCollected);
            Assert.Equal(60m + 20m - 10m, r.CashIn);
            Assert.Equal(100m + 70m - 5m, r.ExpectedCash);
            Assert.Null(r.CountedCash);

            var cash = r.Tenders.Single(t => t.Method == "Cash");
            Assert.Equal(3, cash.Count);
            Assert.Equal(50m + 20m - 10m, cash.Amount);
            Assert.Equal(25.95m, r.Tenders.Single(t => t.Method == "Card").Amount);
        }

        [Fact]
        public void ZReport_ShowsCountedCashAndOverShort()
        {
            var shift = new Shift { OpenedAt = new DateTime(2026, 10, 2, 8, 0, 0), ClosedAt = new DateTime(2026, 10, 2, 17, 0, 0), OpeningBalance = 100m, ActualClosingBalance = 145m };
            var r = ShiftReportBuilder.Build(shift, new[] { Sale(1, 50m) }, 0m, DateTime.MaxValue);

            Assert.True(r.IsZ);
            Assert.Equal(shift.ClosedAt, r.To);
            Assert.Equal(150m, r.ExpectedCash);
            Assert.Equal(-5m, r.OverShort);
        }
    }

    /// <summary>The sales tax report.</summary>
    public sealed class TaxReportTests
    {
        private static Sale Taxed(decimal billDiscount, params (decimal Total, decimal Rate, decimal Tax)[] lines)
        {
            var sale = new Sale { DiscountOnBill = billDiscount };
            foreach (var l in lines) sale.SaleItems.Add(new SaleItem { Total = l.Total, TaxRate = l.Rate, TaxAmount = l.Tax });
            sale.TaxTotal = lines.Sum(l => l.Tax);
            sale.TotalBill = lines.Sum(l => l.Total) - billDiscount + sale.TaxTotal;
            return sale;
        }

        [Fact]
        public void SplitsSales_IntoTaxableByRate_NonTaxable_AndExempt()
        {
            var a = Taxed(0m, (10m, 8.25m, 0.83m), (5m, 0m, 0m));
            var b = Taxed(4m, (30m, 8.25m, 2.23m), (10m, 0m, 0m));           // discount shared 3 / 1
            var exempt = Taxed(0m, (20m, 0m, 0m));
            exempt.IsTaxExempt = true;
            var ret = Taxed(0m, (-10m, 8.25m, -0.83m));
            ret.SaleType = "Return";

            var r = TaxReportBuilder.Build(new[] { a, b, exempt, ret }, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

            var rate = Assert.Single(r.Rates);
            Assert.Equal(8.25m, rate.RatePercent);
            Assert.Equal(10m + 27m - 10m, rate.TaxableSales);
            Assert.Equal(0.83m + 2.23m - 0.83m, rate.Tax);
            Assert.Equal(5m + 9m, r.NonTaxableSales);
            Assert.Equal(20m, r.ExemptSales);
            Assert.Equal(2.23m, r.TaxCollected);
            Assert.Equal(r.TaxableSales + r.NonTaxableSales + r.ExemptSales + r.TaxCollected, r.TotalSales);
        }

        [Theory]
        [InlineData("ThisMonth", "2026-10-01", "2026-10-31")]
        [InlineData("LastMonth", "2026-09-01", "2026-09-30")]
        [InlineData("ThisQuarter", "2026-10-01", "2026-12-31")]
        [InlineData("LastQuarter", "2026-07-01", "2026-09-30")]
        [InlineData("ThisYear", "2026-01-01", "2026-12-31")]
        public void Periods_MatchTheCalendar(string name, string from, string to)
        {
            var (f, t) = TaxReportDialog.PeriodFor(name, new DateTime(2026, 10, 2));
            Assert.Equal(DateTime.Parse(from), f);
            Assert.Equal(DateTime.Parse(to), t);
        }
    }

    /// <summary>Returns can't refund the same units twice.</summary>
    public sealed class ReturnOnceTests
    {
        [Fact]
        public void ReturnableQuantity_IsWhatIsLeft()
        {
            var item = new ReturnItemViewModel { OriginalQuantity = 3 };
            item.AlreadyReturned = 2;
            Assert.Equal(1, item.ReturnableQuantity);
            item.AlreadyReturned = 5;
            Assert.Equal(0, item.ReturnableQuantity);
        }

        [Fact]
        public void DrawerKick_IsTheStandardEscPosPulse()
        {
            Assert.Equal(new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }, RawPrinter.DrawerKick());
            Assert.Equal(0x01, RawPrinter.DrawerKick(pin5: true)[2]);
        }
    }

    /// <summary>Repository pieces against a real migrated database.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class RetailDatabaseTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static Sale Return(string invoice, string? original, string? note, string productId, decimal qty) => new()
        {
            InvoiceNumber = invoice, SaleType = "Return", SaleDate = new DateTime(2026, 10, 2), OriginalInvoiceNumber = original, BillNote = note,
            SaleItems = { new SaleItem { ProductId = productId, ProductName = productId, Quantity = -qty } }
        };

        [Fact]
        public async Task EarlierReturns_AreFoundByLink_AndByTheOldNote_WithoutMatchingOtherInvoices()
        {
            using var db = _dir.NewMigratedContext();
            db.Sales.AddRange(
                Return("R-1", "1101", null, "P1", 1),
                Return("R-2", null, "Return for Invoice: 1101", "P1", 1),                 // older build, no link
                Return("R-3", null, "Return for Invoice: 1101. Reason: damaged", "P2", 1),
                Return("R-4", null, "Return for Invoice: 11015", "P1", 5),                // a different invoice
                Return("R-5", "11015", null, "P1", 1));
            await db.SaveChangesAsync();

            var found = await new SaleRepository(db).GetReturnsForInvoiceAsync("1101");
            Assert.Equal(new[] { "R-1", "R-2", "R-3" }, found.Select(s => s.InvoiceNumber).OrderBy(n => n));
            Assert.All(found, s => Assert.NotEmpty(s.SaleItems));
        }

        [Fact]
        public async Task ShiftActivity_IsLimitedToTheShiftsTime()
        {
            using var db = _dir.NewMigratedContext();
            var from = new DateTime(2026, 10, 2, 8, 0, 0);
            db.Sales.Add(new Sale { InvoiceNumber = "1", SaleDate = from.AddHours(1), TotalBill = 10m });
            db.Sales.Add(new Sale { InvoiceNumber = "2", SaleDate = from.AddHours(-1), TotalBill = 99m });
            db.Expenses.Add(new Expense { Description = "Ice", Amount = 4m, Date = from.AddHours(2) });
            db.Expenses.Add(new Expense { Description = "Old", Amount = 50m, Date = from.AddDays(-1) });
            await db.SaveChangesAsync();

            var activity = await new ShiftRepository(db).GetActivityAsync(from, from.AddHours(10));
            Assert.Equal("1", Assert.Single(activity.Sales).InvoiceNumber);
            Assert.Equal(4m, activity.Expenses);
        }

        [Fact]
        public async Task AutoBackup_OnlyWhenOn_OncePerDay_AndKeepsTheNewest14()
        {
            using var db = _dir.NewMigratedContext();
            var backup = new AutoBackupService(db);

            Assert.Null(await backup.RunIfDueAsync());          // off by default

            await backup.SetEnabledAsync(true);
            var first = await backup.RunIfDueAsync();
            Assert.NotNull(first);
            Assert.True(File.Exists(first));
            Assert.Null(await backup.RunIfDueAsync());          // not again the same day

            // Twenty older copies: the next run keeps only the newest 14.
            for (var i = 0; i < 20; i++)
            {
                var old = Path.Combine(backup.Folder, $"posapp_auto_2026010{i % 10}_00000{i % 10}{i:00}.db");
                File.WriteAllText(old, "x");
                File.SetLastWriteTime(old, DateTime.Now.AddDays(-30));
            }
            File.SetLastWriteTime(first!, DateTime.Now.AddDays(-2));
            Assert.NotNull(await backup.RunIfDueAsync());
            Assert.Equal(AutoBackupService.KeepCount, Directory.GetFiles(backup.Folder, "posapp_auto_*.db").Length);
        }
    }
}
