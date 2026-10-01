using POSApp.UI.ViewModels;

namespace POSApp.Tests
{
    /// <summary>
    /// A flat-amount line discount used to be refunded as if it were a percentage
    /// (e.g. Rs. 25 off a Rs. 350 item refunded 262.50 instead of 325.00).
    /// </summary>
    public sealed class SaleReturnRefundTests
    {
        private static ReturnItemViewModel Line(decimal price, decimal qty, string type, decimal discount) => new()
        {
            ProductId = "P1",
            ProductName = "Item",
            OriginalQuantity = (int)qty,
            LineQuantity = qty,
            UnitPrice = price,
            DiscountType = type,
            DiscountPercent = discount
        };

        [Fact]
        public void FlatDiscount_WholeLineReturned_RefundsWhatWasPaid()
        {
            var item = Line(350, 1, "AMT", 25);
            item.ReturnQuantity = 1;
            Assert.Equal(325m, item.Total);
        }

        [Fact]
        public void FlatDiscount_IsSharedOutPerReturnedUnit()
        {
            var item = Line(100, 3, "AMT", 30);
            item.ReturnQuantity = 1;
            Assert.Equal(90m, item.Total);
            item.ReturnQuantity = 3;
            Assert.Equal(270m, item.Total);
        }

        [Fact]
        public void FlatDiscount_ThatDoesNotDivideEvenly_RoundsPerPartAndIsExactWhenAllAreReturned()
        {
            var item = Line(100, 3, "AMT", 10);
            item.ReturnQuantity = 1;
            Assert.Equal(96.67m, item.Total);   // 10 / 3 = 3.33 of the discount
            item.ReturnQuantity = 3;
            Assert.Equal(290m, item.Total);     // the full 10 — no rounding residue
        }

        [Fact]
        public void LegacyPkrDiscountType_CountsAsAFlatAmount()
        {
            var item = Line(350, 1, "PKR", 25);
            item.ReturnQuantity = 1;
            Assert.Equal(325m, item.Total);
        }

        [Fact]
        public void PercentDiscount_IsUnchanged()
        {
            var item = Line(1250.5m, 2, "%", 10);
            item.ReturnQuantity = 1;
            Assert.Equal(1125.45m, item.Total);
        }

        [Fact]
        public void NothingReturned_RefundsNothing()
        {
            Assert.Equal(0m, Line(100, 3, "AMT", 30).Total);
        }
    }
}
