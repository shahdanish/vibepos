using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class DashboardViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly IExpenseRepository _expenseRepository;

        private decimal _todaySales;
        private decimal _todayExpenses;
        private decimal _todayProfit;
        private int _lowStockCount;

        public ObservableCollection<TopSellingItem> TopSellingProducts { get; } = new();
        public ObservableCollection<LowStockItem> LowStockProducts { get; } = new();

        public decimal TodaySales
        {
            get => _todaySales;
            set => SetProperty(ref _todaySales, value);
        }

        public decimal TodayExpenses
        {
            get => _todayExpenses;
            set => SetProperty(ref _todayExpenses, value);
        }

        public decimal TodayProfit
        {
            get => _todayProfit;
            set => SetProperty(ref _todayProfit, value);
        }

        public int LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        public ICommand RefreshCommand { get; }

        public DashboardViewModel(ISaleRepository saleRepository, IProductRepository productRepository, IExpenseRepository expenseRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _expenseRepository = expenseRepository;

            RefreshCommand = new RelayCommand(async _ => await LoadDashboard());

            _ = LoadDashboard();
        }

        private async Task LoadDashboard()
        {
            try
            {
                // Today's sales
                var todaySales = await _saleRepository.GetByDateAsync(DateTime.Now.Date);
                TodaySales = todaySales.Sum(s => s.TotalBill);

                // Today's expenses
                TodayExpenses = await _expenseRepository.GetTotalByDateAsync(DateTime.Now.Date);

                // Today's profit (revenue - cost - expenses)
                var todayCost = todaySales.Sum(s => s.SaleItems.Sum(si => si.CostPrice * si.Quantity));
                TodayProfit = TodaySales - todayCost - TodayExpenses;

                // Low stock products
                var lowStock = await _productRepository.GetLowStockProductsAsync();
                LowStockProducts.Clear();
                foreach (var product in lowStock)
                {
                    LowStockProducts.Add(new LowStockItem
                    {
                        ProductName = product.ProductName,
                        Stock = product.Stock,
                        Threshold = product.MinStockThreshold
                    });
                }
                LowStockCount = LowStockProducts.Count;

                // Top selling products (last 30 days)
                var recentItems = await _saleRepository.GetRecentSalesItemsAsync(30);
                var topSellers = recentItems
                    .GroupBy(si => si.ProductName)
                    .Select(g => new TopSellingItem
                    {
                        ProductName = g.Key,
                        TotalQuantity = g.Sum(si => si.Quantity),
                        TotalRevenue = g.Sum(si => si.Total)
                    })
                    .OrderByDescending(t => t.TotalQuantity)
                    .Take(10);

                TopSellingProducts.Clear();
                foreach (var item in topSellers)
                {
                    TopSellingProducts.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public sealed class TopSellingItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public sealed class LowStockItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Threshold { get; set; }
    }
}
