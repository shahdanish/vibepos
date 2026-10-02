namespace POSApp.Core.Services
{
    /// <summary>One cart line going into the tax calculation.</summary>
    /// <param name="LineTotal">The line after its own discount (what the cart shows).</param>
    /// <param name="RatePercent">The line's sales-tax rate, e.g. 8.25.</param>
    public sealed record TaxLineInput(decimal LineTotal, decimal RatePercent);

    /// <param name="Taxable">The line after its share of the bill discount.</param>
    public sealed record TaxLineResult(decimal Taxable, decimal RatePercent, decimal Tax);

    /// <summary>Tax for one rate, as printed on a US receipt ("Sales tax 8.25%").</summary>
    public sealed record TaxRateTotal(decimal RatePercent, decimal Taxable, decimal Tax);

    public sealed record SaleTaxResult(
        IReadOnlyList<TaxLineResult> Lines,
        decimal LinesTotal,
        decimal BillDiscount,
        decimal TaxTotal,
        decimal Total)
    {
        /// <summary>Lines total minus the bill discount: the pre-tax amount.</summary>
        public decimal Subtotal => LinesTotal - BillDiscount;

        public IReadOnlyList<TaxRateTotal> ByRate =>
            Lines.Where(l => l.Tax != 0)
                 .GroupBy(l => l.RatePercent)
                 .OrderBy(g => g.Key)
                 .Select(g => new TaxRateTotal(g.Key, g.Sum(l => l.Taxable), g.Sum(l => l.Tax)))
                 .ToList();
    }

    /// <summary>
    /// US sales tax for a cart (pure, no I/O).
    ///
    /// A bill discount lowers the taxable amount: it is shared out over the lines in proportion
    /// to their totals (the last line takes the rounding remainder, so the shares add up
    /// exactly). Each line is then taxed at its own rate and rounded to the cent, half away
    /// from zero. Storing tax per line lets a return refund exactly the tax that was paid.
    ///
    /// With every rate at 0 the total is the lines total minus the bill discount, which is
    /// exactly how the sale screen has always added up a bill.
    /// </summary>
    public static class SaleTaxCalculator
    {
        public static SaleTaxResult Calculate(IReadOnlyList<TaxLineInput> lines, decimal billDiscount, bool taxExempt)
        {
            var linesTotal = lines.Sum(l => l.LineTotal);
            var shares = ShareDiscount(lines, billDiscount, linesTotal);

            var results = new List<TaxLineResult>(lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                var taxable = Math.Max(0m, lines[i].LineTotal - shares[i]);
                var rate = taxExempt ? 0m : lines[i].RatePercent;
                var tax = rate > 0 ? Math.Round(taxable * rate / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
                results.Add(new TaxLineResult(taxable, rate, tax));
            }

            var taxTotal = results.Sum(r => r.Tax);
            return new SaleTaxResult(results, linesTotal, billDiscount, taxTotal, linesTotal - billDiscount + taxTotal);
        }

        private static decimal[] ShareDiscount(IReadOnlyList<TaxLineInput> lines, decimal billDiscount, decimal linesTotal)
        {
            var shares = new decimal[lines.Count];
            if (billDiscount <= 0 || linesTotal <= 0) return shares;

            var last = -1;
            for (var i = 0; i < lines.Count; i++)
                if (lines[i].LineTotal > 0) last = i;

            var given = 0m;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].LineTotal <= 0) continue;
                shares[i] = i == last
                    ? billDiscount - given
                    : Math.Round(billDiscount * lines[i].LineTotal / linesTotal, 2, MidpointRounding.AwayFromZero);
                given += shares[i];
            }
            return shares;
        }
    }
}
