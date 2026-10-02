using Microsoft.EntityFrameworkCore;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.Infrastructure.Repositories;
using POSApp.Infrastructure.SampleData;
using POSApp.Infrastructure.Services;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.Tests
{
    /// <summary>The pure US sales-tax calculation.</summary>
    public sealed class SaleTaxCalculatorTests
    {
        private static SaleTaxResult Calc(decimal billDiscount, bool exempt, params (decimal Total, decimal Rate)[] lines) =>
            SaleTaxCalculator.Calculate(lines.Select(l => new TaxLineInput(l.Total, l.Rate)).ToList(), billDiscount, exempt);

        [Fact]
        public void WithoutTax_TheTotalIsTheOriginalSum()
        {
            // The representative Pakistani cart: line totals minus both bill discounts.
            var r = Calc(5m + 10.25m, false, (46m, 0m), (3376.35m, 0m), (325m, 0m));
            Assert.Equal(0m, r.TaxTotal);
            Assert.Equal(3732.10m, r.Total);
        }

        [Fact]
        public void TaxIsRoundedPerLine_HalfAwayFromZero()
        {
            var r = Calc(0m, false, (10m, 8.25m)); // 0.825
            Assert.Equal(0.83m, r.TaxTotal);
            Assert.Equal(10.83m, r.Total);
        }

        [Fact]
        public void BillDiscount_LowersTheTaxableAmount_InProportion()
        {
            var r = Calc(4m, false, (30m, 8.25m), (10m, 0m));
            Assert.Equal(27m, r.Lines[0].Taxable);
            Assert.Equal(9m, r.Lines[1].Taxable);
            Assert.Equal(2.23m, r.TaxTotal);       // 27 × 8.25% = 2.2275
            Assert.Equal(38.23m, r.Total);         // 40 − 4 + 2.23
            Assert.Equal(36m, r.Subtotal);
        }

        [Fact]
        public void DiscountShares_AddUpExactly()
        {
            var r = Calc(1m, false, (10m, 10m), (10m, 10m), (10m, 10m));
            Assert.Equal(29m, r.Lines.Sum(l => l.Taxable));
            Assert.Equal(new[] { 9.67m, 9.67m, 9.66m }, r.Lines.Select(l => l.Taxable));
        }

        [Fact]
        public void DiscountBiggerThanTheBill_NeverMakesNegativeTax()
        {
            var r = Calc(50m, false, (20m, 8m));
            Assert.Equal(0m, r.TaxTotal);
            Assert.All(r.Lines, l => Assert.True(l.Taxable >= 0));
        }

        [Fact]
        public void ExemptCustomer_PaysNoTax()
        {
            var r = Calc(0m, true, (10m, 8.25m), (5m, 6m));
            Assert.Equal(0m, r.TaxTotal);
            Assert.Equal(15m, r.Total);
        }

        [Fact]
        public void ByRate_GroupsForTheReceipt()
        {
            var r = Calc(0m, false, (10m, 8.25m), (20m, 8.25m), (4m, 2m), (3m, 0m));
            var rates = r.ByRate;
            Assert.Equal(2, rates.Count);
            Assert.Equal(2m, rates[0].RatePercent);
            Assert.Equal(0.08m, rates[0].Tax);
            Assert.Equal(8.25m, rates[1].RatePercent);
            Assert.Equal(30m, rates[1].Taxable);
            Assert.Equal(0.83m + 1.65m, rates[1].Tax);
        }
    }

    /// <summary>Taking payment on a US bill.</summary>
    public sealed class TenderCalculatorTests
    {
        [Fact]
        public void Cash_CanBeMoreThanDue_AndGivesChange()
        {
            var (p, error) = TenderCalculator.Take(PaymentMethods.Cash, 50m, 45.95m);
            Assert.Null(error);
            Assert.Equal(45.95m, p!.Amount);
            Assert.Equal(50m, p.Tendered);
            Assert.Equal(4.05m, p.Change);
        }

        [Theory]
        [InlineData(PaymentMethods.Card)]
        [InlineData(PaymentMethods.Check)]
        [InlineData(PaymentMethods.ChargeAccount)]
        public void OtherTenders_CannotExceedTheAmountDue(string method)
        {
            var (p, error) = TenderCalculator.Take(method, 50m, 45.95m);
            Assert.Null(p);
            Assert.NotNull(error);
        }

        [Theory]
        [InlineData("123")]
        [InlineData("12a4")]
        [InlineData("12345")]
        public void CardLast4_MustBeFourDigits(string last4)
        {
            Assert.NotNull(TenderCalculator.Take(PaymentMethods.Card, 10m, 10m, cardLast4: last4).Error);
        }

        [Fact]
        public void SingleCash_WithNothingTyped_IsTheExactAmount()
        {
            var (p, error) = TenderCalculator.Single(PaymentMethods.Cash, 15.83m, null);
            Assert.Null(error);
            Assert.Equal(15.83m, p!.Tendered);
            Assert.Equal(0m, p.Change);
        }

        [Fact]
        public void SingleCash_ShortOfTheTotal_IsRefused()
        {
            Assert.NotNull(TenderCalculator.Single(PaymentMethods.Cash, 15.83m, 10m).Error);
        }

        [Fact]
        public void Remaining_AndChange_AcrossSeveralTenders()
        {
            var card = TenderCalculator.Take(PaymentMethods.Card, 25.95m, 45.95m).Payment!;
            var cash = TenderCalculator.Take(PaymentMethods.Cash, 30m, 20m).Payment!;
            Assert.Equal(0m, TenderCalculator.Remaining(45.95m, new[] { card, cash }));
            Assert.Equal(10m, TenderCalculator.Change(new[] { card, cash }));
        }

        [Fact]
        public void Describe_ShowsCardAndCheckDetails_ButNeverAFullNumber()
        {
            var card = TenderCalculator.Take(PaymentMethods.Card, 10m, 10m, "Visa", "4242", "A1B2C3").Payment!;
            var check = TenderCalculator.Take(PaymentMethods.Check, 10m, 10m, reference: "1045").Payment!;
            Assert.Equal("Card (Visa ****4242 Auth A1B2C3)", TenderCalculator.Describe(card));
            Assert.Equal("Check (#1045)", TenderCalculator.Describe(check));
        }

        [Fact]
        public void DrawerCash_CountsCashTendersOnly_AndKeepsTheOldFigureWithoutTenders()
        {
            var legacy = new Sale { TotalBill = 50m, ReceiveCash = 60m };
            Assert.Equal(60m, DrawerCash.For(legacy, s => s.ReceiveCash));

            var split = new Sale { TotalBill = 45.95m, ReceiveCash = 54.05m };
            split.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = 20m, Tendered = 28.10m });
            split.Payments.Add(new SalePayment { Method = PaymentMethods.Card, Amount = 25.95m, Tendered = 25.95m });
            Assert.Equal(20m, DrawerCash.For(split, s => s.ReceiveCash));

            var refund = new Sale { TotalBill = -10m };
            refund.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = -10m, Tendered = -10m });
            Assert.Equal(-10m, DrawerCash.For(refund, s => s.ReceiveCash));
        }

        [Fact]
        public void TaxSettings_RateFollowsTheProductsCategory_OrTheDefault()
        {
            var settings = new TaxSettings(true, new[]
            {
                new TaxCategory { Id = 1, Name = "General", RatePercent = 8.25m, IsDefault = true },
                new TaxCategory { Id = 2, Name = "Rx", RatePercent = 0m }
            });
            Assert.Equal(8.25m, settings.RateFor(null));
            Assert.Equal(0m, settings.RateFor(2));
            Assert.Equal(8.25m, settings.RateFor(99));        // unknown category: the default
            Assert.Equal(0m, settings with { Enabled = false } is var off ? off.RateFor(null) : -1);
        }
    }

    /// <summary>The US sale screen: tax on the bill, tenders on the saved sale, the receipt.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class UsCheckoutTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public UsCheckoutTests() => Region.Save(RegionSettingsData.UnitedStates());

        public void Dispose() => Region.Save(_original);

        private static readonly Product Bandages = new() { Id = 1, ProductId = "P1", ProductName = "Adhesive Bandages", UnitPrice = 10m, Stock = 50 };
        private static readonly Product Acetaminophen = new() { Id = 2, ProductId = "P2", ProductName = "Acetaminophen 500 mg", UnitPrice = 5m, Stock = 50, TaxCategoryId = 2 };

        private static ITaxRepository Tax(bool enabled = true)
        {
            var tax = new Mock<ITaxRepository>();
            tax.Setup(r => r.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new TaxSettings(enabled, new[]
            {
                new TaxCategory { Id = 1, Name = "General merchandise", RatePercent = 8.25m, IsDefault = true },
                new TaxCategory { Id = 2, Name = "Non-prescription drugs (OTC)", RatePercent = 0m }
            }));
            return tax.Object;
        }

        private static SaleViewModel Screen(List<Sale>? saved = null, List<Customer>? updated = null, bool taxOn = true)
        {
            var vm = SaleFixtures.NewSaleScreen(saved: saved, tax: Tax(taxOn), catalogue: new[] { Bandages, Acetaminophen }, updatedCustomers: updated);
            vm.AddProductToCart(Bandages);       // 10.00, taxed 8.25%
            vm.AddProductToCart(Acetaminophen);  // 5.00, OTC at 0%
            return vm;
        }

        [Fact]
        public void Bill_AddsTax_ByEachProductsCategory()
        {
            Sta.Run(() =>
            {
                var vm = Screen();
                Assert.True(vm.TaxApplies);
                Assert.Equal(15m, vm.Subtotal);
                Assert.Equal(0.83m, vm.TaxTotal);
                Assert.Equal(15.83m, vm.TotalBill);
                Assert.Equal("Sales tax 8.25%", vm.TaxLabel);
                Assert.Equal(PaymentMethods.UnitedStates, vm.PaymentTypes);
            });
        }

        [Fact]
        public void TaxSwitchedOff_LeavesTheBillUntaxed()
        {
            Sta.Run(() =>
            {
                var vm = Screen(taxOn: false);
                Assert.False(vm.TaxApplies);
                Assert.Equal(15m, vm.TotalBill);
            });
        }

        [Fact]
        public void ExemptCustomer_PaysNoTax_AndTheCertificateIsRecorded()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = Screen(saved);
                vm.SelectedCustomer = new Customer { Id = 7, Name = "Resale Co", IsTaxExempt = true, TaxExemptNumber = "EX-123" };

                Assert.Equal(0m, vm.TaxTotal);
                Assert.Equal(15m, vm.TotalBill);
                Assert.Equal("Sales tax (exempt)", vm.TaxLabel);

                vm.SaveCommand.Execute(null);
                var sale = Assert.Single(saved);
                Assert.Equal("EX-123", sale.TaxExemptNumber);
                Assert.Equal(0m, sale.TaxTotal);
            });
        }

        [Fact]
        public void CashSale_IsSavedWithItsTaxAndTender()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = Screen(saved);
                vm.ReceiveCash = 20m;
                vm.SaveCommand.Execute(null);

                var sale = Assert.Single(saved);
                Assert.Equal(15.83m, sale.TotalBill);
                Assert.Equal(0.83m, sale.TaxTotal);
                Assert.Equal(new[] { 0.83m, 0m }, sale.SaleItems.Select(i => i.TaxAmount));
                Assert.Equal(new[] { 8.25m, 0m }, sale.SaleItems.Select(i => i.TaxRate));
                var cash = Assert.Single(sale.Payments);
                Assert.Equal(PaymentMethods.Cash, cash.Method);
                Assert.Equal(15.83m, cash.Amount);
                Assert.Equal(20m, cash.Tendered);
                Assert.Equal(4.17m, sale.Balance); // change
            });
        }

        [Fact]
        public void SplitPayment_SavesEveryTender()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = Screen(saved);
                vm.ApplySplitPayments(new[]
                {
                    TenderCalculator.Take(PaymentMethods.Cash, 5m, 15.83m).Payment!,
                    TenderCalculator.Take(PaymentMethods.Card, 10.83m, 10.83m, "Visa", "4242").Payment!
                });
                Assert.True(vm.IsSplitPayment);
                Assert.Equal("Split: Cash $5.00 + Card $10.83", vm.SplitSummary);

                vm.SaveCommand.Execute(null);
                var sale = Assert.Single(saved);
                Assert.Equal(PaymentMethods.Split, sale.PaymentType);
                Assert.Equal(new[] { PaymentMethods.Cash, PaymentMethods.Card }, sale.Payments.Select(p => p.Method));
                Assert.Equal("4242", sale.Payments[1].CardLast4);
                Assert.False(vm.IsSplitPayment); // next sale starts clean
            });
        }

        [Fact]
        public void ChangingTheBill_DropsASplitEnteredForTheOldTotal()
        {
            Sta.Run(() =>
            {
                var vm = Screen();
                vm.ApplySplitPayments(new[] { TenderCalculator.Take(PaymentMethods.Card, 15.83m, 15.83m).Payment! });
                vm.AddProductToCart(Acetaminophen);
                Assert.False(vm.IsSplitPayment);
            });
        }

        [Fact]
        public void ChargeAccount_PutsTheBillOnTheCustomersAccount()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var updated = new List<Customer>();
                var vm = Screen(saved, updated);
                var customer = new Customer { Id = 9, Name = "Linda Thompson", CurrentBalance = 24.50m };
                vm.SelectedCustomer = customer;
                vm.PaymentType = PaymentMethods.ChargeAccount;
                Assert.True(vm.IsCreditPayment);

                vm.SaveCommand.Execute(null);
                Assert.Equal(PaymentMethods.ChargeAccount, Assert.Single(Assert.Single(saved).Payments).Method);
                Assert.Same(customer, Assert.Single(updated));
                Assert.Equal(24.50m + 15.83m, customer.CurrentBalance);
            });
        }

        [Fact]
        public void Receipt_ShowsTaxTendersAndChange()
        {
            var text = Sta.Run(() =>
            {
                var vm = Screen();
                vm.ReceiveCash = 20m;
                Assert.True(vm.ResolvePayments());
                return Sta.TextOf(vm.CreateProfessionalInvoice());
            });

            Assert.Contains("\tSubtotal\t15.00\n", text);
            Assert.Contains("\tSales tax 8.25%\t0.83\n", text);
            Assert.Contains("\tTOTAL\t$15.83\n", text);
            Assert.Contains("\tCash\t20.00\n", text);
            Assert.Contains("\tChange\t$4.17\n", text);
        }
    }

    /// <summary>Refunding tax on a US return.</summary>
    public sealed class UsReturnTaxTests
    {
        [Theory]
        [InlineData(1, 0.83)]  // half of 1.65 = 0.825
        [InlineData(2, 1.65)]  // the whole line: exactly what was charged
        [InlineData(0, 0)]
        public void ReturnedUnits_GetTheirShareOfTheTax(int returned, decimal expected)
        {
            var item = new ReturnItemViewModel
            {
                ProductId = "P1", UnitPrice = 10m, OriginalQuantity = 2, LineQuantity = 2,
                DiscountType = "%", LineTaxAmount = 1.65m, TaxRate = 8.25m
            };
            item.ReturnQuantity = returned;
            Assert.Equal(expected, item.ReturnedTax);
        }

        [Fact]
        public void TotalRefund_IncludesTheTax()
        {
            Sta.Run(() =>
            {
                var vm = new SaleReturnViewModel(new Mock<ISaleRepository>().Object, new Mock<IProductRepository>().Object);
                var item = new ReturnItemViewModel
                {
                    ProductId = "P1", UnitPrice = 10m, OriginalQuantity = 2, LineQuantity = 2, DiscountType = "%", LineTaxAmount = 1.65m
                };
                vm.ReturnItems.Add(item);
                item.ReturnQuantity = 1;

                Assert.Equal(0.83m, vm.TaxRefund);
                Assert.Equal(10.83m, vm.TotalReturnAmount);
            });
        }

        [Fact]
        public void RefundGoesBack_TheWayTheSaleWasPaid()
        {
            static Sale Paid(params string[] methods)
            {
                var sale = new Sale();
                foreach (var m in methods) sale.Payments.Add(new SalePayment { Method = m });
                return sale;
            }

            Assert.Equal(PaymentMethods.Card, SaleReturnViewModel.RefundMethodFor(Paid(PaymentMethods.Card)));
            Assert.Equal(PaymentMethods.Cash, SaleReturnViewModel.RefundMethodFor(Paid(PaymentMethods.Card, PaymentMethods.Cash)));
            Assert.Equal(PaymentMethods.Card, SaleReturnViewModel.RefundMethodFor(Paid(PaymentMethods.Card, PaymentMethods.ChargeAccount)));
            Assert.Equal(PaymentMethods.Cash, SaleReturnViewModel.RefundMethodFor(Paid()));
        }
    }

    /// <summary>Tax settings, sample data and the drawer, against a real migrated database.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class SalesTaxDatabaseTests : IDisposable
    {
        private readonly TempDataDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        [Fact]
        public async Task UsDefaults_AreCreatedOnce_AndSwitchTaxOnForARate()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new TaxRepository(db);

            await repo.EnsureUsDefaultsAsync(8.25m);
            await repo.EnsureUsDefaultsAsync(8.25m);

            var settings = await repo.GetSettingsAsync();
            Assert.True(settings.Enabled);
            Assert.Equal(5, settings.Categories.Count);
            Assert.Equal(TaxRepository.GeneralCategoryName, settings.Default!.Name);
            Assert.Equal(8.25m, settings.Default.RatePercent);
            Assert.Equal(0m, settings.Categories.Single(c => c.Name == "Prescription drugs").RatePercent);
        }

        [Fact]
        public async Task ZeroRate_CreatesCategories_ButLeavesTaxOff()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new TaxRepository(db);
            await repo.EnsureUsDefaultsAsync(0m);

            var settings = await repo.GetSettingsAsync();
            Assert.False(settings.Enabled);
            Assert.Equal(5, settings.Categories.Count);
        }

        [Fact]
        public async Task Save_UpdatesAddsAndRemoves_AndProductsOfARemovedCategoryUseTheDefault()
        {
            using var db = _dir.NewMigratedContext();
            var repo = new TaxRepository(db);
            await repo.EnsureUsDefaultsAsync(7m);
            var before = (await repo.GetSettingsAsync()).Categories;
            var food = before.Single(c => c.Name == "Food & grocery");

            var product = await db.Products.FirstAsync();
            product.TaxCategoryId = food.Id;
            await db.SaveChangesAsync();

            var edited = before.Where(c => c.Id != food.Id)
                .Select(c => new TaxCategory { Id = c.Id, Name = c.Name, RatePercent = c.Name == TaxRepository.GeneralCategoryName ? 6.5m : c.RatePercent, IsDefault = c.IsDefault })
                .Append(new TaxCategory { Name = "Candy & soda", RatePercent = 6.5m })
                .ToList();
            await repo.SaveSettingsAsync(enabled: true, edited);

            var after = await repo.GetSettingsAsync();
            Assert.DoesNotContain(after.Categories, c => c.Name == "Food & grocery");
            Assert.Contains(after.Categories, c => c.Name == "Candy & soda" && c.RatePercent == 6.5m);
            Assert.Equal(6.5m, after.Default!.RatePercent);
            Assert.Null((await db.Products.AsNoTracking().FirstAsync(p => p.Id == product.Id)).TaxCategoryId);
        }

        [Fact]
        public async Task SampleMedicines_GetTheOtcTaxCategory()
        {
            using (var db = _dir.NewMigratedContext())
            {
                await new TaxRepository(db).EnsureUsDefaultsAsync(8.25m);
                await new FirstRunSetupService(db).CompleteAsync(new FirstRunSetupRequest("owner", "Owner#2026", LoadSampleData: true));
            }

            using (var db = _dir.NewMigratedContext())
            {
                var otc = await db.TaxCategories.SingleAsync(c => c.Name == TaxRepository.OtcCategoryName);
                Assert.Equal(otc.Id, (await db.Products.SingleAsync(p => p.ProductName == "Ibuprofen 200 mg Tablets, 100 ct")).TaxCategoryId);
                Assert.Null((await db.Products.SingleAsync(p => p.ProductName == "Adhesive Bandages, Assorted Sizes, 100 ct")).TaxCategoryId);
            }
        }

        [Fact]
        public async Task ClosingAShift_CountsOnlyTheCashOfUsSales()
        {
            using var db = _dir.NewMigratedContext();
            var shifts = new ShiftRepository(db);
            var shift = await shifts.OpenShiftAsync(100m);
            var after = shift.OpenedAt.AddMinutes(1);

            // A sale with no tenders: counted as before (ReceiveCash).
            db.Sales.Add(new Sale { InvoiceNumber = "1", SaleDate = after, TotalBill = 50m, ReceiveCash = 60m, Balance = 10m });
            // US split sale: only the 20.00 cash part is in the drawer.
            var split = new Sale { InvoiceNumber = "2", SaleDate = after, TotalBill = 45.95m, ReceiveCash = 50m, Balance = 4.05m };
            split.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = 20m, Tendered = 24.05m });
            split.Payments.Add(new SalePayment { Method = PaymentMethods.Card, Amount = 25.95m, Tendered = 25.95m });
            db.Sales.Add(split);
            // US cash refund: paid out of the drawer.
            var refund = new Sale { InvoiceNumber = "R-3", SaleType = "Return", SaleDate = after, TotalBill = -10m, ReceiveCash = -10m };
            refund.Payments.Add(new SalePayment { Method = PaymentMethods.Cash, Amount = -10m, Tendered = -10m });
            db.Sales.Add(refund);
            await db.SaveChangesAsync();

            await shifts.CloseShiftAsync(shift.Id, 170m);

            var closed = await db.Shifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);
            Assert.Equal(100m + 60m + 20m - 10m, closed.ExpectedClosingBalance);
        }
    }
}
