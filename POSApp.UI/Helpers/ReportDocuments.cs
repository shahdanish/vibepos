using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using POSApp.Core.Services;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Printable reports (end-of-day X/Z, sales tax, …) as FlowDocuments: the shop's header,
    /// a title, then label/amount sections. Shown in <c>ReportViewerWindow</c> and printed from it.
    /// </summary>
    public static class ReportDocuments
    {
        public static FlowDocument ShiftReport(ShiftReport r)
        {
            var doc = NewDocument();
            doc.Blocks.Add(ReceiptBranding.BuildHeader(20, 12));
            doc.Blocks.Add(Title(r.IsZ ? "Z REPORT — shift closed" : "X REPORT — shift still open"));
            doc.Blocks.Add(Note($"{Region.DocumentDateTime(r.From)}  to  {Region.DocumentDateTime(r.To)}\nPrinted {Region.DocumentDateTime(AppClock.Now)}"));

            doc.Blocks.Add(Section("Sales", new (string, string)[]
            {
                ($"Sales ({r.SalesCount})", Region.Money(r.GrossSales)),
                ($"Returns ({r.ReturnsCount})", Minus(r.Returns)),
                ("Net sales", Region.Money(r.NetSales)),
                ("Bill discounts given", Region.Money(r.Discounts)),
                ("Sales tax collected", Region.Money(r.TaxCollected)),
            }, boldLast: false, bold: "Net sales"));

            doc.Blocks.Add(Section("Payments", r.Tenders.Count == 0
                ? new[] { ("No sales", "") }
                : r.Tenders.Select(t => ($"{t.Method} ({t.Count})", Region.Money(t.Amount))).ToArray()));

            var cash = new List<(string, string)>
            {
                ("Opening cash", Region.Money(r.OpeningCash)),
                ("Cash taken (less cash refunds)", Region.Money(r.CashIn)),
                ("Paid out (expenses)", Minus(r.Expenses)),
                ("Expected in drawer", Region.Money(r.ExpectedCash)),
            };
            if (r.CountedCash is decimal counted)
            {
                cash.Add(("Counted", Region.Money(counted)));
                var diff = r.OverShort ?? 0;
                cash.Add((diff >= 0 ? "Over" : "Short", Region.Money(Math.Abs(diff))));
            }
            doc.Blocks.Add(Section("Cash drawer", cash.ToArray(), bold: "Expected in drawer"));
            return doc;
        }

        public static FlowDocument TaxReport(TaxReport r)
        {
            var doc = NewDocument();
            doc.Blocks.Add(ReceiptBranding.BuildHeader(20, 12));
            doc.Blocks.Add(Title("SALES TAX REPORT"));
            doc.Blocks.Add(Note($"{Region.Date(r.From)}  to  {Region.Date(r.To)}  ·  {r.Transactions} transactions (returns included as negatives)\nPrinted {Region.DocumentDateTime(AppClock.Now)}"));

            var rows = new List<(string, string)> { ("Total sales (incl. tax)", Region.Money(r.TotalSales)) };
            foreach (var rate in r.Rates)
                rows.Add(($"Taxable sales at {rate.RatePercent:0.###}%", Region.Money(rate.TaxableSales)));
            rows.Add(("Non-taxable sales", Region.Money(r.NonTaxableSales)));
            rows.Add(("Tax-exempt sales", Region.Money(r.ExemptSales)));
            doc.Blocks.Add(Section("Sales", rows.ToArray()));

            var tax = r.Rates.Select(rate => ($"Tax at {rate.RatePercent:0.###}%", Region.Money(rate.Tax))).ToList();
            tax.Add(("Total tax collected", Region.Money(r.TaxCollected)));
            doc.Blocks.Add(Section("Tax", tax.ToArray(), bold: "Total tax collected"));

            doc.Blocks.Add(Note("Figures come from the sales recorded in this program. Check them against your own records before filing; " +
                                "this report does not file anything for you."));
            return doc;
        }

        // ── Building blocks ─────────────────────────────────────────────────────

        /// <summary>An amount taken off, shown with a minus sign (but never "-$0.00").</summary>
        private static string Minus(decimal amount) => amount == 0 ? Region.Money(0) : "-" + Region.Money(amount);

        public static FlowDocument NewDocument() => new()
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(24),
            ColumnWidth = double.PositiveInfinity,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        public static Paragraph Title(string text) => new(new Run(text))
        {
            FontSize = 15, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 2)
        };

        public static Paragraph Note(string text) => new(new Run(text))
        {
            FontSize = 10, Foreground = Brushes.DimGray, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8)
        };

        /// <summary>A heading and two columns of label / value rows.</summary>
        public static Section Section(string heading, IReadOnlyList<(string Label, string Value)> rows, bool boldLast = false, string? bold = null)
        {
            var section = new Section { Margin = new Thickness(0, 6, 0, 4) };
            section.Blocks.Add(new Paragraph(new Run(heading.ToUpperInvariant()))
            {
                FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 2),
                BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 1)
            });

            var table = new Table { CellSpacing = 0 };
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            var group = new TableRowGroup();
            for (var i = 0; i < rows.Count; i++)
            {
                var strong = (boldLast && i == rows.Count - 1) || rows[i].Label == bold;
                var row = new TableRow();
                row.Cells.Add(Cell(rows[i].Label, TextAlignment.Left, strong));
                row.Cells.Add(Cell(rows[i].Value, TextAlignment.Right, strong));
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);
            section.Blocks.Add(table);
            return section;
        }

        private static TableCell Cell(string text, TextAlignment align, bool bold) =>
            new(new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal })
            {
                Padding = new Thickness(2, 1, 2, 1)
            };
    }
}
