using System.Windows;
using POSApp.Core.Services;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class ShiftWindow : Window
    {
        public ShiftWindow(ShiftViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ShowReport = report => new ReportViewerWindow(
                report.IsZ ? "Z Report" : "X Report",
                () => ReportDocuments.ShiftReport(report),
                () => ShiftReportCsv(report),
                $"{(report.IsZ ? "z" : "x")}-report-{report.To:yyyy-MM-dd-HHmm}.csv") { Owner = this }.ShowDialog();
        }

        private static IEnumerable<IEnumerable<string>> ShiftReportCsv(ShiftReport r)
        {
            yield return new[] { "Item", "Count", "Amount" };
            yield return new[] { "Period from", Csv.DateTime(r.From), "" };
            yield return new[] { "Period to", Csv.DateTime(r.To), "" };
            yield return new[] { "Sales", r.SalesCount.ToString(), Csv.Money(r.GrossSales) };
            yield return new[] { "Returns", r.ReturnsCount.ToString(), Csv.Money(-r.Returns) };
            yield return new[] { "Net sales", "", Csv.Money(r.NetSales) };
            yield return new[] { "Bill discounts", "", Csv.Money(r.Discounts) };
            yield return new[] { "Sales tax collected", "", Csv.Money(r.TaxCollected) };
            foreach (var t in r.Tenders)
                yield return new[] { "Payments: " + Csv.Text(t.Method), t.Count.ToString(), Csv.Money(t.Amount) };
            yield return new[] { "Opening cash", "", Csv.Money(r.OpeningCash) };
            yield return new[] { "Cash taken (less cash refunds)", "", Csv.Money(r.CashIn) };
            yield return new[] { "Paid out (expenses)", "", Csv.Money(r.Expenses) };
            yield return new[] { "Expected in drawer", "", Csv.Money(r.ExpectedCash) };
            if (r.CountedCash is decimal counted)
            {
                yield return new[] { "Counted", "", Csv.Money(counted) };
                yield return new[] { "Over (+) / short (-)", "", Csv.Money(r.OverShort ?? 0) };
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
