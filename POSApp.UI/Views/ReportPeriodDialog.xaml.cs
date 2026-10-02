using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>Picks a period (this month, last quarter, … or custom dates) and runs a report for it.</summary>
    public partial class ReportPeriodDialog : Window
    {
        private readonly Func<DateTime, DateTime, Task> _show;

        /// <param name="show">Builds and shows the report for the chosen first and last day.</param>
        public ReportPeriodDialog(string title, string subtitle, Func<DateTime, DateTime, Task> show)
        {
            InitializeComponent();
            Title = ShopTitleExtension.Build(title);
            Heading.Text = title;
            Subtitle.Text = subtitle;
            _show = show;
            SetPeriod("ThisMonth");
        }

        /// <summary>The sales tax report: period picker, then the report viewer.</summary>
        public static ReportPeriodDialog ForSalesTax(ISaleRepository sales, Window owner) =>
            new("Sales Tax Report", "Taxable, non-taxable and exempt sales and the tax collected, for your sales-tax return.",
                async (from, to) =>
                {
                    var report = TaxReportBuilder.Build(await sales.GetByDateRangeAsync(from, to), from, to);
                    new ReportViewerWindow("Sales Tax Report", () => ReportDocuments.TaxReport(report), () => TaxCsv(report),
                        $"sales-tax-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv") { Owner = owner }.ShowDialog();
                }) { Owner = owner };

        /// <summary>The pseudoephedrine logbook for a period.</summary>
        public static ReportPeriodDialog ForPseLog(IFrontStoreRepository frontStore, Window owner) =>
            new("PSE Logbook", "Every pseudoephedrine / ephedrine purchase recorded at this shop.",
                async (from, to) =>
                {
                    var entries = await frontStore.GetPseLogAsync(from, to);
                    new ReportViewerWindow("PSE Logbook", () => ReportDocuments.PseLog(entries, from, to), () => ReportDocuments.PseLogCsv(entries),
                        $"pse-log-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv") { Owner = owner }.ShowDialog();
                }) { Owner = owner };

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
                await _show(from, to);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("build the report", ex.Message);
            }
        }

        private static IEnumerable<IEnumerable<string>> TaxCsv(TaxReport r)
        {
            yield return new[] { "Item", "Rate %", "Amount" };
            yield return new[] { "Period from", "", Csv.Date(r.From) };
            yield return new[] { "Period to", "", Csv.Date(r.To) };
            yield return new[] { "Total sales (incl. tax)", "", Csv.Money(r.TotalSales) };
            foreach (var rate in r.Rates)
                yield return new[] { "Taxable sales", Csv.Number(rate.RatePercent), Csv.Money(rate.TaxableSales) };
            yield return new[] { "Non-taxable sales", "", Csv.Money(r.NonTaxableSales) };
            yield return new[] { "Tax-exempt sales", "", Csv.Money(r.ExemptSales) };
            foreach (var rate in r.Rates)
                yield return new[] { "Tax collected", Csv.Number(rate.RatePercent), Csv.Money(rate.Tax) };
            yield return new[] { "Total tax collected", "", Csv.Money(r.TaxCollected) };
        }
    }
}
