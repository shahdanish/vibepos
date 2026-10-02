using POSApp.Core.Entities;

namespace POSApp.Core.Services
{
    /// <summary>Taxable sales and tax for one rate in a period.</summary>
    public sealed record TaxReportRate(decimal RatePercent, decimal TaxableSales, decimal Tax);

    /// <summary>
    /// Sales tax for a period, in the shape a sales-tax return asks for: total sales, what was
    /// taxable at each rate, what was not taxable, what was sold tax-exempt, and the tax
    /// collected. Returns are included as negatives, so every figure is net.
    /// </summary>
    public sealed record TaxReport(
        DateTime From,
        DateTime To,
        decimal TotalSales,
        IReadOnlyList<TaxReportRate> Rates,
        decimal NonTaxableSales,
        decimal ExemptSales,
        decimal TaxCollected,
        int Transactions)
    {
        public decimal TaxableSales => Rates.Sum(r => r.TaxableSales);
    }

    /// <summary>Builds <see cref="TaxReport"/> from saved sales (pure).</summary>
    public static class TaxReportBuilder
    {
        public static TaxReport Build(IEnumerable<Sale> sales, DateTime from, DateTime to)
        {
            var list = sales.ToList();
            var rates = new Dictionary<decimal, (decimal Taxable, decimal Tax)>();
            decimal nonTaxable = 0, exempt = 0;

            foreach (var sale in list)
            {
                var taxable = TaxableByLine(sale);
                if (sale.IsTaxExempt)
                {
                    exempt += taxable.Sum();
                    continue;
                }

                for (var i = 0; i < sale.SaleItems.Count; i++)
                {
                    var item = sale.SaleItems[i];
                    if (item.TaxRate > 0)
                    {
                        rates.TryGetValue(item.TaxRate, out var r);
                        rates[item.TaxRate] = (r.Taxable + taxable[i], r.Tax + item.TaxAmount);
                    }
                    else
                    {
                        nonTaxable += taxable[i];
                    }
                }
            }

            return new TaxReport(
                from, to,
                TotalSales: list.Sum(s => s.TotalBill),
                Rates: rates.OrderBy(r => r.Key).Select(r => new TaxReportRate(r.Key, r.Value.Taxable, r.Value.Tax)).ToList(),
                NonTaxableSales: nonTaxable,
                ExemptSales: exempt,
                TaxCollected: list.Sum(s => s.TaxTotal),
                Transactions: list.Count);
        }

        /// <summary>
        /// Each line after its share of the bill discount, exactly as the sale screen worked it
        /// out (the same calculator, so the shares match). Lines of a return have no bill
        /// discount and stay negative.
        /// </summary>
        private static decimal[] TaxableByLine(Sale sale)
        {
            var billDiscount = sale.DiscountOnBill + sale.DiscountOnProducts;
            if (billDiscount == 0)
                return sale.SaleItems.Select(i => i.Total).ToArray();

            var result = SaleTaxCalculator.Calculate(
                sale.SaleItems.Select(i => new TaxLineInput(i.Total, 0m)).ToList(), billDiscount, taxExempt: false);
            return result.Lines.Select(l => l.Taxable).ToArray();
        }
    }
}
