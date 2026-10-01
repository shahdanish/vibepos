using System.Globalization;
using Moq;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;
using POSApp.Core.Services;

namespace POSApp.Tests
{
    /// <summary>
    /// Freezes what an existing Pakistani till prints and charges today. The golden text was
    /// captured from the code BEFORE the US localization work began; the localization layer
    /// must keep these byte-identical under the default (PK) settings. If one of these fails,
    /// a change has altered what live PK customers see on paper — fix the change, not the test.
    /// </summary>
    [Collection(GlobalStateCollection.Name)]
    public sealed class PakistanReceiptCharacterizationTests : IDisposable
    {
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
        private readonly RegionSettingsData _original = Region.Current;

        public PakistanReceiptCharacterizationTests() => Region.Apply(new RegionSettingsData());

        public void Dispose() => Region.Apply(_original);

        [Fact]
        public void Totals_KeepTodaysUnroundedMath()
        {
            var vm = Sta.Run(() => { var s = SaleFixtures.NewSaleScreen(); SaleFixtures.FillCart(s); return s; }, EnUs);

            // 2×23 + (3×1250.5 − 10%) + (350 − 25) − 5 − 10.25
            Assert.Equal(46m + 3376.35m + 325m - 5m - 10.25m, vm.TotalBill);
            Assert.Equal(3732.10m, vm.TotalBill);
            Assert.Equal(4000m - 3732.10m, vm.Balance);
            Assert.Equal(375.15m + 25m, vm.TotalItemsDiscount);
        }

        [Fact]
        public void SaleReceipt_A4_PrintsExactlyAsBefore()
        {
            var text = Sta.Run(() =>
            {
                var vm = SaleFixtures.NewSaleScreen();
                SaleFixtures.FillCart(vm);
                return Sta.TextOf(vm.CreateProfessionalInvoice());
            }, EnUs);

            Assert.Equal(ExpectedSaleReceipt, text);
        }

        [Fact]
        public void ReturnReceipt_PrintsExactlyAsBefore()
        {
            var text = Sta.Run(() =>
            {
                var vm = new SaleReturnViewModel(new Mock<ISaleRepository>().Object, new Mock<IProductRepository>().Object);
                vm.OriginalSale = new Sale
                {
                    InvoiceNumber = "11042",
                    CustomerName = "Ahmed",
                    SaleDate = new DateTime(2026, 10, 1, 9, 5, 0),
                    SaleItems =
                    {
                        new SaleItem { ProductId = "101124", ProductName = "Glycerin 25gm", Quantity = 4, UnitPrice = 23, CostPrice = 18 },
                        new SaleItem { ProductId = "2002", ProductName = "Cooking Oil 1L", Quantity = 2, UnitPrice = 1250.5m, CostPrice = 400, DiscountPercent = 10 }
                    }
                };
                vm.ReturnInvoiceNumber = "R-11051";
                vm.ReturnDate = new DateTime(2026, 10, 14, 16, 5, 0);
                vm.ReturnItems[0].ReturnQuantity = 1;
                vm.ReturnItems[1].ReturnQuantity = 1;
                vm.TotalReturnAmount = vm.ReturnItems.Sum(i => i.Total);
                return Sta.TextOf(vm.CreateReturnReceipt());
            }, EnUs);

            Assert.Equal(ExpectedReturnReceipt, text);
        }

        private const string ExpectedSaleReceipt =
            "Your Store Name\n" +
            "Your Store Address\n" +
            "Bill / Invoice\n" +
            "Bill No: 11050\tDate: 14-Oct-2026 02:35 PM\n" +
            "Customer: Cash\n" +
            "Address: \tMobile: \n" +
            "S.No\tProduct Name\tQty\tPrice\tDisc\tTotal\n" +
            "1\tGlycerin 25gm\t2\t23\t\t46.00\n" +
            "2\tCooking Oil 1L\t3\t1,251\t375\t3,376.35\n" +
            "3\tSugar 5kg\t1\t350\t25\t325.00\n" +
            "\tItem Discounts\t400.15\n" +
            "\tTotal Discount\t415.40\n" +
            "\tTotal Bill\t3,732.10\n" +
            "\tCash Received\t4,000.00\n" +
            "\tTotal Items Quantity\t6\n" +
            "\tBalance Amount\t267.90\n" +
            "Thank You For Your Business!\n" +
            "Please keep this invoice for your records.\n";

        private const string ExpectedReturnReceipt =
            "Your Store Name\n" +
            "Your Store Address\n" +
            "Return Receipt\n" +
            "Return No: R-11051\tDate: 14-Oct-2026 04:05 PM\n" +
            "Orig Invoice: 11042\tCustomer: Ahmed\n" +
            "---------------------------------------------\n" +
            "S.No\tProduct Name\tQty\tPrice\tDisc\tTotal\n" +
            "1\tGlycerin 25gm\t1\t23\t0\t23.00\n" +
            "2\tCooking Oil 1L\t1\t1,251\t10\t1,125.45\n" +
            "Total Items Qty\t2\n" +
            "Total Refund\t1,148.45\n" +
            "Thank You For Your Business!\n" +
            "Please keep this invoice for your records.\n";
    }
}
