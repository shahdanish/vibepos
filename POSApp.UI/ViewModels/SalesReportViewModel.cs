using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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
                MessageBox.Show("Please enter an invoice number.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var sale = await _saleRepository.GetByInvoiceNumberAsync(InvoiceNumber);
                Sales.Clear();

                if (sale != null)
                {
                    Sales.Add(sale);
                    UpdateSummary();
                    MessageBox.Show($"Invoice {InvoiceNumber} found!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UpdateSummary();
                    MessageBox.Show($"Invoice {InvoiceNumber} not found.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching invoice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SearchByDateRange()
        {
            if (EndDate < StartDate)
            {
                MessageBox.Show("End date cannot be before start date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                {
                    Sales.Add(sale);
                }

                UpdateSummary();

                if (!sales.Any())
                {
                    MessageBox.Show("No sales found for the selected period.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sales: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Please select a sale to print.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Print invoice logic
                var invoice = GenerateInvoiceText(SelectedSale);
                MessageBox.Show($"Print Invoice Feature\n\n{invoice}\n\nNote: Connect to printer for actual printing.",
                    "Print Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing invoice: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport()
        {
            if (!Sales.Any())
            {
                MessageBox.Show("No sales data to print.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var report = GenerateReportText();
            MessageBox.Show($"Print Report Feature\n\n{report}\n\nNote: Connect to printer for actual printing.",
                "Print Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportToExcel()
        {
            if (!Sales.Any())
            {
                MessageBox.Show("No sales data to export.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Excel Export Feature - To be implemented with Excel library.",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private string GenerateInvoiceText(Sale sale)
        {
            var invoice = $"SHAH JEE SUPER STORE\n";
            invoice += $"================================\n";
            invoice += $"Invoice: {sale.InvoiceNumber}\n";
            invoice += $"Date: {sale.SaleDate:dd/MM/yyyy HH:mm}\n";
            invoice += $"Customer: {sale.CustomerName}\n";
            invoice += $"Type: {sale.SaleType}\n";
            invoice += $"================================\n\n";
            invoice += $"Items:\n";

            foreach (var item in sale.SaleItems)
            {
                invoice += $"{item.ProductName}\n";
                invoice += $"  {item.Quantity} x Rs.{item.UnitPrice:N2} = Rs.{item.Total:N2}\n";
            }

            invoice += $"\n================================\n";
            invoice += $"Total Bill: Rs.{sale.TotalBill:N2}\n";
            invoice += $"Received: Rs.{sale.ReceiveCash:N2}\n";
            invoice += $"Balance: Rs.{sale.Balance:N2}\n";
            invoice += $"================================\n";

            return invoice;
        }

        private string GenerateReportText()
        {
            var report = $"{ReportTitle}\n";
            report += $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}\n";
            report += $"================================\n";
            report += $"Total Sales: {TotalSales}\n";
            report += $"Total Revenue: Rs.{TotalRevenue:N2}\n";
            report += $"Total Cost: Rs.{TotalCost:N2}\n";
            report += $"Total Profit: Rs.{TotalProfit:N2}\n";
            report += $"Total Discount: Rs.{TotalDiscount:N2}\n";
            report += $"================================\n";

            return report;
        }
    }
}
