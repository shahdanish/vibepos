using System.Globalization;
using POSApp.Core.Entities;
using POSApp.Core.Services;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.Tests
{
    /// <summary>The rule that decides when a sale happened (pure).</summary>
    public sealed class SaleTimeTests
    {
        private static readonly DateTime Now = new(2026, 10, 14, 14, 35, 0);

        [Fact]
        public void ScreenOpenedYesterday_SaleIsRecordedNow()
        {
            // The sale window was opened yesterday morning and hidden, not closed, overnight.
            var shown = new DateTime(2026, 10, 13, 9, 0, 0);
            Assert.Equal(Now, SaleTime.Resolve(shown, chosenByUser: false, Now));
        }

        [Fact]
        public void CashierPickedToday_SaleIsRecordedNow()
        {
            // The date picker hands back midnight of the chosen day.
            Assert.Equal(Now, SaleTime.Resolve(Now.Date, chosenByUser: true, Now));
        }

        [Fact]
        public void CashierBackdated_KeepsThatDayAtTheCurrentTime()
        {
            var picked = new DateTime(2026, 10, 10);
            Assert.Equal(new DateTime(2026, 10, 10, 14, 35, 0), SaleTime.Resolve(picked, chosenByUser: true, Now));
        }
    }

    /// <summary>The sale screen records the real time and a fresh invoice number.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class SaleScreenSaveTests : IDisposable
    {
        private readonly TimeProvider _originalClock = AppClock.Provider;
        private readonly FixedClock _clock = new(new DateTime(2026, 10, 13, 9, 0, 0)); // screen opened yesterday

        public SaleScreenSaveTests() => AppClock.Provider = _clock;

        public void Dispose() => AppClock.Provider = _originalClock;

        private static void AddOneItem(SaleViewModel vm) =>
            vm.SaleItems.Add(new SaleItemViewModel { ProductId = "P1", ProductName = "Item", Quantity = 1, UnitPrice = 10 });

        [Fact]
        public void Save_RecordsTheTimeOfSaving_AndAFreshInvoiceNumber()
        {
            Sta.Run(() =>
            {
                var numbers = new Queue<string>(new[] { "11050", "11052", "11053" });
                var saved = new List<Sale>();
                var vm = SaleFixtures.NewSaleScreen(() => numbers.Dequeue(), saved);
                Assert.Equal("11050", vm.InvoiceNumber);

                AddOneItem(vm);
                _clock.Now = new DateTime(2026, 10, 14, 14, 35, 0);
                vm.SaveCommand.Execute(null);

                var sale = Assert.Single(saved);
                Assert.Equal(new DateTime(2026, 10, 14, 14, 35, 0), sale.SaleDate);
                // 11051 was taken by the other sale window meanwhile; the number is re-read at save time.
                Assert.Equal("11052", sale.InvoiceNumber);

                // The next sale starts from "now", not from the old screen-open time.
                Assert.Equal(_clock.Now, vm.SaleDate);
                Assert.Equal("11053", vm.InvoiceNumber);
            });
        }

        [Fact]
        public void Save_KeepsADeliberatelyBackdatedDay()
        {
            Sta.Run(() =>
            {
                var saved = new List<Sale>();
                var vm = SaleFixtures.NewSaleScreen(saved: saved);
                AddOneItem(vm);

                vm.SaleDate = new DateTime(2026, 10, 10); // cashier enters last Saturday's paper sales
                _clock.Now = new DateTime(2026, 10, 14, 14, 35, 0);
                vm.SaveCommand.Execute(null);

                Assert.Equal(new DateTime(2026, 10, 10, 14, 35, 0), Assert.Single(saved).SaleDate);
            });
        }
    }

    /// <summary>The existing bill layout, printed with the US preset: cents, US date, US time.</summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class UsReceiptFormattingTests : IDisposable
    {
        private readonly RegionSettingsData _original = Region.Current;

        public UsReceiptFormattingTests() => Region.Apply(RegionSettingsData.UnitedStates());

        public void Dispose() => Region.Apply(_original);

        [Fact]
        public void Bill_ShowsCentsAndUsDate()
        {
            // Run under a German machine culture to prove nothing leaks from the PC's settings.
            var text = Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen();
                SaleFixtures.FillCart(vm);
                return Sta.TextOf(vm.CreateProfessionalInvoice());
            }, CultureInfo.GetCultureInfo("de-DE"));

            Assert.Contains("Date: 10/14/2026 2:35 PM", text);
            Assert.Contains("2\tCooking Oil 1L\t3\t1,250.50\t375.15\t3,376.35\n", text);
            Assert.Contains("1\tGlycerin 25gm\t2\t23.00\t\t46.00\n", text);
            // US receipt: subtotal, bill discount, then the total with the currency symbol.
            Assert.Contains("\tSubtotal\t3,747.35\n", text);
            Assert.Contains("\tDiscount\t-15.25\n", text);
            Assert.Contains("\tTOTAL\t$3,732.10\n", text);
            Assert.DoesNotContain("Total Bill", text);
        }

        [Fact]
        public void ShopPhone_IsFormattedForTheUs_AndPrintedAsTypedElsewhere()
        {
            var original = ReceiptBranding.Current;
            try
            {
                ReceiptBranding.Save(new ReceiptBrandingSettings { StoreName = "Test Shop", StorePhone = "5555550142" });

                string Bill() => Sta.Run(() =>
                {
                    var vm = SaleFixtures.NewSaleScreen();
                    SaleFixtures.FillCart(vm);
                    return Sta.TextOf(vm.CreateProfessionalInvoice());
                });

                Assert.Contains("(555) 555-0142", Bill());

                Region.Apply(RegionSettingsData.Pakistan());
                var pk = Bill();
                Assert.Contains("5555550142", pk);
                Assert.DoesNotContain("(555)", pk);
            }
            finally
            {
                ReceiptBranding.Save(original);
            }
        }
    }
}
