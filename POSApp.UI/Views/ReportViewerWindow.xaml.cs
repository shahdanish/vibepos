using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using POSApp.Core.Services;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Shows a report on screen and prints it (straight to the default printer, like every
    /// other printout). Optionally exports the same figures as CSV for the accountant.
    /// </summary>
    public partial class ReportViewerWindow : Window
    {
        private readonly Func<FlowDocument> _build;
        private readonly Func<IEnumerable<IEnumerable<string>>>? _csvRows;
        private readonly string _csvFileName;

        /// <param name="build">Builds the document (called again for printing, as a document can only be shown once).</param>
        /// <param name="csvRows">Rows for "Export CSV", or null to hide the button.</param>
        public ReportViewerWindow(string title, Func<FlowDocument> build,
                                  Func<IEnumerable<IEnumerable<string>>>? csvRows = null, string csvFileName = "report.csv")
        {
            InitializeComponent();
            Title = ShopTitleExtension.Build(title);
            _build = build;
            _csvRows = csvRows;
            _csvFileName = csvFileName;
            ExportButton.Visibility = csvRows == null ? Visibility.Collapsed : Visibility.Visible;
            Viewer.Document = build();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = _build();
                var dialog = new PrintDialog();
                if (SettingsManager.LoadSettings().UseSmallBillFormat)
                {
                    doc.PageWidth = 280;
                    doc.ColumnWidth = 260;
                    doc.PagePadding = new Thickness(5);
                }
                else
                {
                    doc.PageWidth = dialog.PrintableAreaWidth;
                    doc.PageHeight = dialog.PrintableAreaHeight;
                    doc.ColumnWidth = dialog.PrintableAreaWidth;
                }
                dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, Title);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print the report", ex.Message);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_csvRows == null) return;
            var dialog = new SaveFileDialog
            {
                FileName = _csvFileName,
                Filter = "CSV file (opens in Excel)|*.csv",
                DefaultExt = ".csv"
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                Csv.Write(dialog.FileName, _csvRows());
                NotificationHelper.ShowSuccess($"Saved {System.IO.Path.GetFileName(dialog.FileName)}.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("export the report", ex.Message);
            }
        }
    }
}
