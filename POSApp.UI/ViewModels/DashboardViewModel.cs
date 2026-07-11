using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.UI.Views;

namespace POSApp.UI.ViewModels
{
    public sealed class DashboardViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICloudBackupService _cloudBackup;

        private decimal _todaySales;
        private int _lowStockCount;

        public ObservableCollection<TopSellingItem> TopSellingProducts { get; } = new();
        public ObservableCollection<LowStockItem> LowStockProducts { get; } = new();

        public decimal TodaySales
        {
            get => _todaySales;
            set => SetProperty(ref _todaySales, value);
        }

        public int LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set => SetProperty(ref _isSyncing, value);
        }

        public event Action<CloudBackupResult>? CloudResultReady;

        public ICommand RefreshCommand { get; }
        public ICommand OpenDemandOrderCommand { get; }
        public ICommand BackupToCloudCommand { get; }
        public ICommand RestoreFromCloudCommand { get; }

        public DashboardViewModel(ISaleRepository saleRepository, IProductRepository productRepository,
            ICloudBackupService cloudBackup)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _cloudBackup = cloudBackup;

            RefreshCommand           = new RelayCommand(async _ => await LoadDashboard());
            OpenDemandOrderCommand   = new RelayCommand(_ => OpenDemandOrder());
            BackupToCloudCommand     = new RelayCommand(async _ => await BackupToCloud(), _ => !IsSyncing);
            RestoreFromCloudCommand  = new RelayCommand(async _ => await RestoreFromCloud(), _ => !IsSyncing);

            _ = LoadDashboard();
        }

        private async Task BackupToCloud()
        {
            IsSyncing = true;
            try
            {
                var result = await _cloudBackup.BackupToCloudAsync();
                CloudResultReady?.Invoke(result);
            }
            catch (Exception ex)
            {
                CloudResultReady?.Invoke(new CloudBackupResult { Success = false, ErrorMessage = ex.Message });
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private async Task RestoreFromCloud()
        {
            // Confirm — restore OVERWRITES all current local data.
            var info = await _cloudBackup.GetLatestSnapshotInfoAsync();
            if (info is null)
            {
                CloudResultReady?.Invoke(new CloudBackupResult
                {
                    IsRestore = true,
                    SkipReason = "No cloud backup was found for this store (or the device is offline / not configured)."
                });
                return;
            }

            var confirm = MessageBox.Show(
                $"This will OVERWRITE ALL current local data with the last cloud backup taken on " +
                $"{info.SnapshotTime:dd-MMM-yyyy hh:mm tt}.\n\n" +
                "A safety copy of the current database is saved as posapp.db.prerestore first.\n\n" +
                "Do you want to continue?",
                "Confirm Restore from Cloud",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsSyncing = true;
            try
            {
                var result = await _cloudBackup.RestoreFromCloudAsync();
                CloudResultReady?.Invoke(result);
            }
            catch (Exception ex)
            {
                CloudResultReady?.Invoke(new CloudBackupResult { Success = false, IsRestore = true, ErrorMessage = ex.Message });
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private void OpenDemandOrder()
        {
            if (!LowStockProducts.Any())
            {
                NotificationHelper.ValidationErrorCustom("No low-stock items to create a demand order for.");
                return;
            }

            var items = LowStockProducts.Select(p => new DemandOrderItem
            {
                ProductName   = p.ProductName,
                Barcode       = p.Barcode,
                CurrentStock  = p.Stock,
                MinThreshold  = p.Threshold,
                CostPrice     = p.CostPrice,
                OrderQuantity = Math.Max(1, p.Threshold - p.Stock + 5)
            }).ToList();

            var dialog = new DemandOrderDialog(items);
            dialog.Show();
        }

        private async Task LoadDashboard()
        {
            try
            {
                var todaySales = await _saleRepository.GetByDateAsync(DateTime.Now.Date);
                TodaySales = todaySales.Sum(s => s.TotalBill);

                var lowStock = await _productRepository.GetLowStockProductsAsync();
                LowStockProducts.Clear();
                foreach (var product in lowStock)
                {
                    LowStockProducts.Add(new LowStockItem
                    {
                        ProductName = product.ProductName,
                        Barcode     = product.Barcode,
                        Stock       = product.Stock,
                        Threshold   = product.MinStockThreshold,
                        CostPrice   = product.CostPrice
                    });
                }
                LowStockCount = LowStockProducts.Count;

                var recentItems = await _saleRepository.GetRecentSalesItemsAsync(30);
                var topSellers = recentItems
                    .GroupBy(si => si.ProductName)
                    .Select(g => new TopSellingItem
                    {
                        ProductName   = g.Key,
                        TotalQuantity = g.Sum(si => si.Quantity),
                        TotalRevenue  = g.Sum(si => si.Total)
                    })
                    .OrderByDescending(t => t.TotalQuantity)
                    .Take(10);

                TopSellingProducts.Clear();
                foreach (var item in topSellers)
                    TopSellingProducts.Add(item);
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
        public decimal TotalQuantity { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public sealed class LowStockItem
    {
        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Threshold { get; set; }
        public decimal CostPrice { get; set; }
        public bool IsOutOfStock => Stock <= 0;
    }

    public sealed class DemandOrderItem : ViewModelBase
    {
        private int _orderQuantity;
        private decimal _costPrice;
        private bool _isSelected = true;

        /// <summary>Serial number shown in the grid/print (assigned by the dialog).</summary>
        public int SerialNo { get; set; }

        /// <summary>Row checkbox — only checked rows are printed. Defaults to checked.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinThreshold { get; set; }
        public int OrderQuantity
        {
            get => _orderQuantity;
            set
            {
                if (SetProperty(ref _orderQuantity, value))
                    OnPropertyChanged(nameof(LineTotal));
            }
        }

        /// <summary>Purchase/cost price per unit (price paid to supplier).</summary>
        public decimal CostPrice
        {
            get => _costPrice;
            set
            {
                if (SetProperty(ref _costPrice, value))
                    OnPropertyChanged(nameof(LineTotal));
            }
        }

        /// <summary>Line amount for this row = quantity to order × cost price.</summary>
        public decimal LineTotal => OrderQuantity * CostPrice;
    }
}
