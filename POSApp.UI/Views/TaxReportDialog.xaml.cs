using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>Picks a period and opens the sales tax report for it.</summary>
    public partial class TaxReportDialog : Window
    {
        private readonly ISaleRepository _sales;

        public TaxReportDialog(ISaleRepository sales)
        {
            InitializeComponent();
            _sales = sales;
            SetPeriod("ThisMonth");
        }

        /// <summary>The first and last day of a named period, relative to <paramref name="today"/>.</summary>
        public static (DateTime From, DateTime To) PeriodFor(string name, DateTime today)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var quarterStart = new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1);
            return name switch
            {
                "LastMonth" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
                "ThisQuarter" => (quarterStart, quarterStart.AddMonths(3).AddDays(-1)),
                "LastQuarter" => (quarterStart.AddMonths(-3), quarterStart.AddDays(-1)),
                "ThisYear" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
                _ => (monthStart, monthStart.AddMonths(1).AddDays(-1)),
            };
        }

        private void SetPeriod(string name)
        {
            var (from, to) = PeriodFor(name, AppClock.Now.Date);
            FromDate.SelectedDate = from;
            ToDate.SelectedDate = to;
        }

        private void Period_Click(object sender, RoutedEventArgs e) =>
            SetPeriod((string)((FrameworkElement)sender).Tag);

        private async void Show_Click(object sender, RoutedEventArgs e)
        {
            if (FromDate.SelectedDate is not DateTime from || ToDate.SelectedDate is not DateTime to || to < from)
            {
                NotificationHelper.ValidationErrorCustom("Pick a From date on or before the To date.");
                return;
            }

            try
            {
                var sales = await _sales.GetByDateRangeAsync(from, to);
                var report = TaxReportBuilder.Build(sales, from, to);
                new ReportViewerWindow("Sales Tax Report",
                    () => ReportDocuments.TaxReport(report),
                    () => Csv(report),
                    $"sales-tax-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("build the sales tax report", ex.Message);
            }
        }

        private static IEnumerable<IEnumerable<string>> Csv(TaxReport r)
        {
            yield return new[] { "Item", "Rate %", "Amount" };
            yield return new[] { "Period from", "", Core.Services.Csv.Date(r.From) };
            yield return new[] { "Period to", "", Core.Services.Csv.Date(r.To) };
            yield return new[] { "Total sales (incl. tax)", "", Core.Services.Csv.Money(r.TotalSales) };
            foreach (var rate in r.Rates)
                yield return new[] { "Taxable sales", Core.Services.Csv.Number(rate.RatePercent), Core.Services.Csv.Money(rate.TaxableSales) };
            yield return new[] { "Non-taxable sales", "", Core.Services.Csv.Money(r.NonTaxableSales) };
            yield return new[] { "Tax-exempt sales", "", Core.Services.Csv.Money(r.ExemptSales) };
            foreach (var rate in r.Rates)
                yield return new[] { "Tax collected", Core.Services.Csv.Number(rate.RatePercent), Core.Services.Csv.Money(rate.Tax) };
            yield return new[] { "Total tax collected", "", Core.Services.Csv.Money(r.TaxCollected) };
        }
    }
}
