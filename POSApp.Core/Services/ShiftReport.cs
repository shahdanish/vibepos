using POSApp.Core.Entities;

namespace POSApp.Core.Services
{
    /// <summary>Money taken one way during a shift ("Card", "Cash", …). Refunds count against it.</summary>
    public sealed record TenderTotal(string Method, int Count, decimal Amount);

    /// <summary>
    /// The end-of-day figures for a shift: an X report while it is open (nothing is reset), a Z
    /// report once it is closed (with the counted cash and over/short).
    /// </summary>
    public sealed record ShiftReport(
        bool IsZ,
        DateTime From,
        DateTime To,
        int SalesCount,
        decimal GrossSales,
        int ReturnsCount,
        decimal Returns,
        decimal Discounts,
        decimal TaxCollected,
        IReadOnlyList<TenderTotal> Tenders,
        decimal OpeningCash,
        decimal CashIn,
        decimal Expenses,
        decimal? CountedCash)
    {
        /// <summary>Sales less refunds.</summary>
        public decimal NetSales => GrossSales - Returns;

        /// <summary>What the drawer should hold: opening cash + cash taken − cash paid out.</summary>
        public decimal ExpectedCash => OpeningCash + CashIn - Expenses;

        /// <summary>Counted minus expected (Z only): positive is over, negative is short.</summary>
        public decimal? OverShort => CountedCash - ExpectedCash;
    }

    /// <summary>Builds <see cref="ShiftReport"/> from the shift's sales (pure).</summary>
    public static class ShiftReportBuilder
    {
        public const string ReturnType = "Return";

        /// <param name="sales">Every sale and return dated from the shift's opening to <paramref name="to"/>.</param>
        /// <param name="expenses">Expenses paid out of the drawer in the same period.</param>
        public static ShiftReport Build(Shift shift, IEnumerable<Sale> sales, decimal expenses, DateTime to)
        {
            var list = sales.ToList();
            var regular = list.Where(s => s.SaleType != ReturnType).ToList();
            var returns = list.Where(s => s.SaleType == ReturnType).ToList();

            var tenders = new Dictionary<string, (HashSet<int> Sales, decimal Amount)>();
            void Add(string method, int saleId, decimal amount)
            {
                if (!tenders.TryGetValue(method, out var t)) t = (new HashSet<int>(), 0m);
                t.Sales.Add(saleId);
                tenders[method] = (t.Sales, t.Amount + amount);
            }

            foreach (var sale in list)
            {
                if (sale.Payments.Count > 0)
                    foreach (var p in sale.Payments) Add(p.Method, sale.Id, p.Amount);
                else
                    Add(string.IsNullOrWhiteSpace(sale.PaymentType) ? "Cash" : sale.PaymentType, sale.Id, sale.TotalBill);
            }

            return new ShiftReport(
                IsZ: shift.ClosedAt.HasValue,
                From: shift.OpenedAt,
                To: shift.ClosedAt ?? to,
                SalesCount: regular.Count,
                GrossSales: regular.Sum(s => s.TotalBill),
                ReturnsCount: returns.Count,
                Returns: -returns.Sum(s => s.TotalBill),
                Discounts: regular.Sum(s => s.DiscountOnBill + s.DiscountOnProducts),
                TaxCollected: list.Sum(s => s.TaxTotal),
                Tenders: tenders.OrderByDescending(t => t.Value.Amount)
                                .Select(t => new TenderTotal(t.Key, t.Value.Sales.Count, t.Value.Amount))
                                .ToList(),
                OpeningCash: shift.OpeningBalance,
                CashIn: list.Sum(s => DrawerCash.For(s, x => x.ReceiveCash)),
                Expenses: expenses,
                CountedCash: shift.ClosedAt.HasValue ? shift.ActualClosingBalance : null);
        }
    }
}
