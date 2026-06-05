using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class SalesReportViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private string _invoiceNumber = string.Empty;
        private DateTime _startDate = DateTime.Now.Date;
        private DateTime _endDate = DateTime.Now.Date;
        private Sale? _selectedSale;
        private string _reportTitle = "All Sales";
        private Visibility _searchPanelVisibility = Visibility.Collapsed;
        private Visibility _invoiceSearchVisibility = Visibility.Collapsed;
        private Visibility _customRangeVisibility = Visibility.Collapsed;
        private Visibility _selectedSaleVisibility = Visibility.Collapsed;

        public ObservableCollection<Sale> Sales { get; } = new();

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public Sale? SelectedSale
        {
            get => _selectedSale;
            set
            {
                if (SetProperty(ref _selectedSale, value))
                {
                    SelectedSaleVisibility = value != null ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        public string ReportTitle
        {
            get => _reportTitle;
            set => SetProperty(ref _reportTitle, value);
        }

        public Visibility SearchPanelVisibility
        {
            get => _searchPanelVisibility;
            set => SetProperty(ref _searchPanelVisibility, value);
        }

        public Visibility InvoiceSearchVisibility
        {
            get => _invoiceSearchVisibility;
            set => SetProperty(ref _invoiceSearchVisibility, value);
        }

        public Visibility CustomRangeVisibility
        {
            get => _customRangeVisibility;
            set => SetProperty(ref _customRangeVisibility, value);
        }

        public Visibility SelectedSaleVisibility
        {
            get => _selectedSaleVisibility;
            set => SetProperty(ref _selectedSaleVisibility, value);
        }

        // Summary Properties
        public int TotalSales => Sales.Count;
        public decimal TotalRevenue => Sales.Sum(s => s.TotalBill);
        public decimal TotalCost => Sales.Sum(s => s.SaleItems.Sum(si => si.CostPrice * si.Quantity));
        public decimal TotalProfit => TotalRevenue - TotalCost;
        public decimal TotalDiscount => Sales.Sum(s => s.DiscountOnBill + s.DiscountOnProducts);

        // Commands
        public ICommand ShowTodayCommand { get; }
        public ICommand ShowThisWeekCommand { get; }
        public ICommand ShowThisMonthCommand { get; }
        public ICommand ShowThisYearCommand { get; }
        public ICommand ShowCustomRangeCommand { get; }
        public ICommand ShowInvoiceSearchCommand { get; }
        public ICommand SearchInvoiceCommand { get; }
        public ICommand SearchDateRangeCommand { get; }
        public ICommand ViewDetailsCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool _isAdmin = false;
        private bool _canExport = false;

        public SalesReportViewModel(ISaleRepository saleRepository)
        {
            _saleRepository = saleRepository;

            // Check if current user is admin
            _isAdmin = SessionManager.IsAdmin;
            _canExport = _isAdmin; // Only admins can export

            ShowTodayCommand = new RelayCommand(async _ => await ShowToday());
            ShowThisWeekCommand = new RelayCommand(async _ => await ShowThisWeek());
            ShowThisMonthCommand = new RelayCommand(async _ => await ShowThisMonth());
            ShowThisYearCommand = new RelayCommand(async _ => await ShowThisYear());
            ShowCustomRangeCommand = new RelayCommand(_ => ShowCustomRange());
            ShowInvoiceSearchCommand = new RelayCommand(_ => ShowInvoiceSearch());
            SearchInvoiceCommand = new RelayCommand(async _ => await SearchByInvoice());
            SearchDateRangeCommand = new RelayCommand(async _ => await SearchByDateRange());
            ViewDetailsCommand = new RelayCommand(sale => ViewDetails(sale as Sale));
            PrintCommand = new RelayCommand(_ => PrintInvoice());
            PrintReportCommand = new RelayCommand(_ => PrintReport());
            ExportCommand = new RelayCommand(_ => ExportToExcel(), _ => _canExport); // Admin only
            RefreshCommand = new RelayCommand(async _ => await RefreshData());

            // If not admin, show warning and close window
            if (!_isAdmin)
            {
                MessageBox.Show("Sales reports are restricted to administrators only.\n\nPlease login with an admin account to access this feature.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);

                // Close the window - this will return to Dashboard automatically
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this);
                    window?.Close();
                });
                return; // Exit constructor without loading data
            }

            // Only load data for admins
            _ = ShowToday();
        }

        private async Task ShowToday()
        {
            ReportTitle = $"📅 Today's Sales - {DateTime.Now:dd/MM/yyyy}";
            HideSearchPanels();
            await LoadSalesByDateRange(DateTime.Now.Date, DateTime.Now.Date);
        }

        private async Task ShowThisWeek()
        {
            var startOfWeek = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(6);
            ReportTitle = $"📆 This Week's Sales ({startOfWeek:dd/MM} - {endOfWeek:dd/MM})";
            HideSearchPanels();
            await LoadSalesByDateRange(startOfWeek, endOfWeek);
        }

        private async Task ShowThisMonth()
        {
            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            ReportTitle = $"📅 This Month's Sales - {DateTime.Now:MMMM yyyy}";
            HideSearchPanels();
            await LoadSalesByDateRange(startOfMonth, endOfMonth);
        }

        private async Task ShowThisYear()
        {
            var startOfYear = new DateTime(DateTime.Now.Year, 1, 1);
            var endOfYear = new DateTime(DateTime.Now.Year, 12, 31);
            ReportTitle = $"📊 This Year's Sales - {DateTime.Now.Year}";
            HideSearchPanels();
            await LoadSalesByDateRange(startOfYear, endOfYear);
        }

        private void ShowCustomRange()
        {
            ReportTitle = "🔍 Custom Date Range";
            SearchPanelVisibility = Visibility.Visible;
            InvoiceSearchVisibility = Visibility.Collapsed;
            CustomRangeVisibility = Visibility.Visible;
        }

        private void ShowInvoiceSearch()
        {
            ReportTitle = "🔎 Invoice Search";
            SearchPanelVisibility = Visibility.Visible;
            InvoiceSearchVisibility = Visibility.Visible;
            CustomRangeVisibility = Visibility.Collapsed;
        }

        private void HideSearchPanels()
        {
            SearchPanelVisibility = Visibility.Collapsed;
            InvoiceSearchVisibility = Visibility.Collapsed;
            CustomRangeVisibility = Visibility.Collapsed;
        }

        private async Task SearchByInvoice()
        {
            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                NotificationHelper.ValidationErrorCustom("Please enter an invoice number.");
                return;
            }

            try
            {
                var sale = await _saleRepository.GetByInvoiceNumberAsync(InvoiceNumber);
                Sales.Clear();

                if (sale != null)
                    Sales.Add(sale);

                UpdateSummary();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("search invoice", ex.Message);
            }
        }

        private async Task SearchByDateRange()
        {
            if (EndDate < StartDate)
            {
                NotificationHelper.ValidationErrorCustom("End date cannot be before start date.");
                return;
            }

            ReportTitle = $"📊 Sales Report ({StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy})";
            await LoadSalesByDateRange(StartDate, EndDate);
        }

        private async Task LoadSalesByDateRange(DateTime start, DateTime end)
        {
            try
            {
                var sales = await _saleRepository.GetByDateRangeAsync(start, end);
                Sales.Clear();

                foreach (var sale in sales.OrderByDescending(s => s.SaleDate))
                    Sales.Add(sale);

                UpdateSummary();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("load sales", ex.Message);
            }
        }

        private void ViewDetails(Sale? sale)
        {
            if (sale != null)
            {
                SelectedSale = sale;
            }
        }

        private void PrintInvoice()
        {
            if (SelectedSale == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a sale to print.");
                return;
            }

            try
            {
                FlowDocument doc = CreateInvoiceDocument(SelectedSale);
                PrintDocument(doc, $"Invoice {SelectedSale.InvoiceNumber}");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print invoice", ex.Message);
            }
        }

        private void PrintReport()
        {
            if (!Sales.Any())
            {
                NotificationHelper.ValidationErrorCustom("No sales data to print.");
                return;
            }

            try
            {
                FlowDocument doc = CreateReportDocument();
                PrintDocument(doc, "Sales Report");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print report", ex.Message);
            }
        }

        private void ExportToExcel()
        {
            NotificationHelper.ValidationErrorCustom("Excel export is not yet implemented.");
        }

        private async Task RefreshData()
        {
            // Refresh current view
            if (ReportTitle.Contains("Today"))
                await ShowToday();
            else if (ReportTitle.Contains("Week"))
                await ShowThisWeek();
            else if (ReportTitle.Contains("Month"))
                await ShowThisMonth();
            else if (ReportTitle.Contains("Year"))
                await ShowThisYear();
            else
                await ShowToday();
        }

        private void UpdateSummary()
        {
            OnPropertyChanged(nameof(TotalSales));
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalCost));
            OnPropertyChanged(nameof(TotalProfit));
            OnPropertyChanged(nameof(TotalDiscount));
        }

        // ---------------------------------------------------------------------
        // Printing — mirrors the Sale screen's thermal/A4 FlowDocument output so
        // reports and invoices look consistent across the app.
        // ---------------------------------------------------------------------

        // Respect the same Small Bill (thermal) preference the Sale screen uses.
        private static bool UseSmallBillFormat => SettingsManager.LoadSettings().UseSmallBillFormat;

        /// <summary>
        /// Renders a FlowDocument and prints it silently (no dialog), using the
        /// same page configuration as the Sale screen's invoice printing.
        /// </summary>
        private static void PrintDocument(FlowDocument doc, string description)
        {
            PrintDialog printDialog = new PrintDialog();

            if (UseSmallBillFormat)
            {
                doc.PageWidth = 280;
                doc.PageHeight = double.NaN;
                doc.ColumnWidth = 260;
                doc.PagePadding = new Thickness(5);
                doc.FontSize = 10;
            }
            else
            {
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                doc.PagePadding = new Thickness(40);
            }

            printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, description);
        }

        /// <summary>Shared store header used at the top of every printout.</summary>
        private static void AddStoreHeader(FlowDocument doc, string title)
        {
            Paragraph header = new Paragraph { Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center };
            header.Inlines.Add(new Bold(new Run("ShahJee Super Store")) { FontSize = 24 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 14 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("0332-3324911") { FontSize = 14 });
            doc.Blocks.Add(header);

            Paragraph titlePara = new Paragraph(new Bold(new Run(title)))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 2, 0, 2)
            };
            doc.Blocks.Add(titlePara);
        }

        private static TableCell Cell(string text, TextAlignment align, bool bold = false)
        {
            var para = new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) };
            if (bold) para.FontWeight = FontWeights.Bold;
            return new TableCell(para)
            {
                BorderThickness = new Thickness(0.5),
                BorderBrush = Brushes.Black,
                Padding = new Thickness(2, 1, 2, 1)
            };
        }

        /// <summary>Builds a single-sale invoice matching the Sale screen layout.</summary>
        private FlowDocument CreateInvoiceDocument(Sale sale)
        {
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextAlignment = TextAlignment.Left
            };

            AddStoreHeader(doc, "Bill / Invoice");

            // Metadata
            doc.Blocks.Add(new Paragraph(new Run($"Bill No: {sale.InvoiceNumber}")) { Margin = new Thickness(0, 1, 0, 0) });
            doc.Blocks.Add(new Paragraph(new Run($"Date: {sale.SaleDate:dd-MMM-yyyy hh:mm tt}")) { Margin = new Thickness(0) });
            doc.Blocks.Add(new Paragraph(new Run($"Customer: {sale.CustomerName}")) { Margin = new Thickness(0) });
            if (!string.IsNullOrWhiteSpace(sale.MobileNumber))
                doc.Blocks.Add(new Paragraph(new Run($"Mobile: {sale.MobileNumber}")) { Margin = new Thickness(0) });
            doc.Blocks.Add(new Paragraph(new Run("---------------------------------------------")) { Margin = new Thickness(0, 1, 0, 1) });

            // Items table
            Table itemsTable = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(0.4, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(2.1, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });

            var itemsGroup = new TableRowGroup();
            var head = new TableRow { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            head.Cells.Add(Cell("S.No", TextAlignment.Center));
            head.Cells.Add(Cell("Product Name", TextAlignment.Left));
            head.Cells.Add(Cell("Qty", TextAlignment.Center));
            head.Cells.Add(Cell("Price", TextAlignment.Right));
            head.Cells.Add(Cell("Total", TextAlignment.Right));
            itemsGroup.Rows.Add(head);

            int serial = 1;
            foreach (var item in sale.SaleItems)
            {
                var row = new TableRow();
                row.Cells.Add(Cell(serial++.ToString(), TextAlignment.Center));
                row.Cells.Add(Cell(item.ProductName, TextAlignment.Left));
                row.Cells.Add(Cell(item.Quantity.ToString(), TextAlignment.Center));
                row.Cells.Add(Cell(item.UnitPrice.ToString("N0"), TextAlignment.Right));
                row.Cells.Add(Cell(item.Total.ToString("N2"), TextAlignment.Right));
                itemsGroup.Rows.Add(row);
            }
            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // Totals
            Table totals = new Table { CellSpacing = 0 };
            totals.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            totals.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var totalsGroup = new TableRowGroup();
            void TotalRow(string label, string value, bool bold = false)
            {
                var r = new TableRow();
                var lp = new Paragraph(new Run(label)) { Margin = new Thickness(0) };
                var vp = new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
                if (bold) { lp.FontWeight = FontWeights.Bold; vp.FontWeight = FontWeights.Bold; }
                r.Cells.Add(new TableCell(lp) { Padding = new Thickness(2, 1, 2, 1) });
                r.Cells.Add(new TableCell(vp) { Padding = new Thickness(2, 1, 2, 1) });
                totalsGroup.Rows.Add(r);
            }
            decimal totalDiscount = sale.DiscountOnBill + sale.DiscountOnProducts;
            TotalRow("Total Bill", sale.TotalBill.ToString("N2"), bold: true);
            if (totalDiscount > 0) TotalRow("Total Discount", totalDiscount.ToString("N2"));
            TotalRow("Cash Received", sale.ReceiveCash.ToString("N2"), bold: true);
            TotalRow("Balance Amount", sale.Balance.ToString("N2"), bold: true);
            totals.RowGroups.Add(totalsGroup);
            doc.Blocks.Add(totals);

            Paragraph footer = new Paragraph { Margin = new Thickness(0, 20, 0, 0), TextAlignment = TextAlignment.Center };
            footer.Inlines.Add(new Bold(new Run("Thank You For Your Business!")) { FontSize = 14 });
            doc.Blocks.Add(footer);

            return doc;
        }

        /// <summary>Builds the multi-sale summary report (the list + totals shown on screen).</summary>
        private FlowDocument CreateReportDocument()
        {
            FlowDocument doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextAlignment = TextAlignment.Left
            };

            // Strip leading emoji/icons from the on-screen title for a clean printout.
            string cleanTitle = new string(ReportTitle.Where(c => c < 128).ToArray()).Trim();
            AddStoreHeader(doc, "Sales Report");

            doc.Blocks.Add(new Paragraph(new Run(cleanTitle)) { TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 1, 0, 0) });
            doc.Blocks.Add(new Paragraph(new Run($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}")) { TextAlignment = TextAlignment.Center, FontSize = 10, Margin = new Thickness(0, 0, 0, 2) });
            doc.Blocks.Add(new Paragraph(new Run("---------------------------------------------")) { Margin = new Thickness(0, 1, 0, 1) });

            // Sales list
            Table table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1.4, GridUnitType.Star) }); // Invoice
            table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) }); // Date
            table.Columns.Add(new TableColumn { Width = new GridLength(1.6, GridUnitType.Star) }); // Customer
            table.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) }); // Items
            table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) }); // Total

            var group = new TableRowGroup();
            var header = new TableRow { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            header.Cells.Add(Cell("Invoice #", TextAlignment.Left));
            header.Cells.Add(Cell("Date", TextAlignment.Left));
            header.Cells.Add(Cell("Customer", TextAlignment.Left));
            header.Cells.Add(Cell("Items", TextAlignment.Center));
            header.Cells.Add(Cell("Total", TextAlignment.Right));
            group.Rows.Add(header);

            foreach (var sale in Sales)
            {
                var row = new TableRow();
                row.Cells.Add(Cell(sale.InvoiceNumber, TextAlignment.Left));
                row.Cells.Add(Cell(sale.SaleDate.ToString("dd/MM/yyyy"), TextAlignment.Left));
                row.Cells.Add(Cell(sale.CustomerName, TextAlignment.Left));
                row.Cells.Add(Cell(sale.SaleItems.Count.ToString(), TextAlignment.Center));
                row.Cells.Add(Cell(sale.TotalBill.ToString("N2"), TextAlignment.Right));
                group.Rows.Add(row);
            }
            table.RowGroups.Add(group);
            doc.Blocks.Add(table);

            // Summary totals
            Table summary = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 0) };
            summary.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
            summary.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var sGroup = new TableRowGroup();
            void SummaryRow(string label, string value, bool bold = false)
            {
                var r = new TableRow();
                var lp = new Paragraph(new Run(label)) { Margin = new Thickness(0) };
                var vp = new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
                if (bold) { lp.FontWeight = FontWeights.Bold; vp.FontWeight = FontWeights.Bold; }
                r.Cells.Add(new TableCell(lp) { Padding = new Thickness(2, 1, 2, 1) });
                r.Cells.Add(new TableCell(vp) { Padding = new Thickness(2, 1, 2, 1) });
                sGroup.Rows.Add(r);
            }
            SummaryRow("Total Sales", TotalSales.ToString(), bold: true);
            SummaryRow("Total Revenue", TotalRevenue.ToString("N2"), bold: true);
            SummaryRow("Total Discount", TotalDiscount.ToString("N2"));
            SummaryRow("Total Cost", TotalCost.ToString("N2"));
            SummaryRow("Total Profit", TotalProfit.ToString("N2"), bold: true);
            summary.RowGroups.Add(sGroup);
            doc.Blocks.Add(summary);

            Paragraph footer = new Paragraph { Margin = new Thickness(0, 20, 0, 0), TextAlignment = TextAlignment.Center };
            footer.Inlines.Add(new Run("--- End of Report ---") { FontSize = 10, Foreground = Brushes.Gray });
            doc.Blocks.Add(footer);

            return doc;
        }
    }
}
