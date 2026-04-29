using System.Windows.Input;
using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.Views;

namespace POSApp.UI.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private string _currentUserInfo = string.Empty;
        private bool _isSyncInProgress;

        public ICommand OpenSaleCommand { get; }
        public ICommand OpenWholeSaleCommand { get; }
        public ICommand OpenSaleReturnCommand { get; }
        public ICommand OpenSalesReportCommand { get; }
        public ICommand OpenProductManagementCommand { get; }
        public ICommand OpenCategoryManagementCommand { get; }
        public ICommand OpenDashboardCommand { get; }
        public ICommand OpenExpenseCommand { get; }
        public ICommand OpenShiftCommand { get; }
        public ICommand OpenCustomerLedgerCommand { get; }
        public ICommand OpenDailySummaryCommand { get; }
        public ICommand OpenPurchaseEntryCommand { get; }
        public ICommand OpenSupplierManagementCommand { get; }
        public ICommand OpenBackupRestoreCommand { get; }
        public ICommand SyncNowCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        public string CurrentUserInfo
        {
            get => _currentUserInfo;
            set => SetProperty(ref _currentUserInfo, value);
        }

        public MainViewModel()
        {
            // Set current user info
            if (SessionManager.CurrentUser != null)
            {
                CurrentUserInfo = $"Logged in as: {SessionManager.CurrentUser.Username} ({SessionManager.CurrentUser.Role})";
            }

            OpenSaleCommand = new RelayCommand(_ => OpenSale());
            OpenWholeSaleCommand = new RelayCommand(_ => OpenWholeSale());
            OpenSaleReturnCommand = new RelayCommand(_ => OpenSaleReturn());
            OpenSalesReportCommand = new RelayCommand(_ => OpenSalesReport());
            OpenProductManagementCommand = new RelayCommand(_ => OpenProductManagement());
            OpenCategoryManagementCommand = new RelayCommand(_ => OpenCategoryManagement());
            OpenDashboardCommand = new RelayCommand(_ => OpenDashboard());
            OpenExpenseCommand = new RelayCommand(_ => OpenExpense());
            OpenShiftCommand = new RelayCommand(_ => OpenShift());
            OpenCustomerLedgerCommand = new RelayCommand(_ => OpenCustomerLedger());
            OpenDailySummaryCommand = new RelayCommand(_ => OpenDailySummary());
            OpenPurchaseEntryCommand = new RelayCommand(_ => OpenPurchaseEntry());
            OpenSupplierManagementCommand = new RelayCommand(_ => OpenSupplierManagement());
            OpenBackupRestoreCommand = new RelayCommand(_ => OpenBackupRestore());
            SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => !_isSyncInProgress);
            LogoutCommand = new RelayCommand(_ => Logout());
            ExitCommand = new RelayCommand(_ => Exit());
        }

        private void OpenSale()
        {
            var saleWindow = App.Services?.GetRequiredService<SaleWindow>();
            saleWindow?.ShowDialog();
        }

        private void OpenWholeSale()
        {
            var wholeSaleWindow = App.Services?.GetRequiredService<WholeSaleWindow>();
            wholeSaleWindow?.ShowDialog();
        }

        private void OpenSaleReturn()
        {
            var saleReturnWindow = App.Services?.GetRequiredService<SaleReturnWindow>();
            saleReturnWindow?.ShowDialog();
        }

        private void OpenSalesReport()
        {
            var reportWindow = App.Services?.GetRequiredService<SalesReportWindow>();
            reportWindow?.ShowDialog();
        }

        private void OpenProductManagement()
        {
            var productWindow = App.Services?.GetRequiredService<ProductManagementWindow>();
            productWindow?.ShowDialog();
        }

        private void OpenCategoryManagement()
        {
            var categoryWindow = App.Services?.GetRequiredService<CategoryManagementWindow>();
            categoryWindow?.ShowDialog();
        }

        private void OpenDashboard()
        {
            var dashboardWindow = App.Services?.GetRequiredService<DashboardWindow>();
            dashboardWindow?.ShowDialog();
        }

        private void OpenExpense()
        {
            var expenseWindow = App.Services?.GetRequiredService<ExpenseWindow>();
            expenseWindow?.ShowDialog();
        }

        private void OpenShift()
        {
            var shiftWindow = App.Services?.GetRequiredService<ShiftWindow>();
            shiftWindow?.ShowDialog();
        }

        private void OpenCustomerLedger()
        {
            var ledgerWindow = App.Services?.GetRequiredService<CustomerLedgerWindow>();
            ledgerWindow?.ShowDialog();
        }

        private void OpenDailySummary()
        {
            if (!PermissionManager.CanAccessDailySummary(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access daily summary.");
                return;
            }
            var dailySummaryWindow = App.Services?.GetRequiredService<DailySummaryWindow>();
            dailySummaryWindow?.ShowDialog();
        }

        private void OpenPurchaseEntry()
        {
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases.");
                return;
            }
            var purchaseWindow = App.Services?.GetRequiredService<PurchaseEntryWindow>();
            purchaseWindow?.ShowDialog();
        }

        private void OpenSupplierManagement()
        {
            if (!PermissionManager.CanManageSuppliers(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to manage suppliers.");
                return;
            }
            var supplierWindow = App.Services?.GetRequiredService<SupplierManagementWindow>();
            supplierWindow?.ShowDialog();
        }

        private void OpenBackupRestore()
        {
            if (!PermissionManager.CanAccess(SessionManager.CurrentUser, "BackupRestore"))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access backup/restore.");
                return;
            }
            var backupWindow = App.Services?.GetRequiredService<BackupRestoreWindow>();
            backupWindow?.ShowDialog();
        }

        // Temporary helper for verifying background sync during development.
        private async Task SyncNowAsync()
        {
            if (_isSyncInProgress)
                return;

            try
            {
                _isSyncInProgress = true;
                var sync = App.Services?.GetService<ISyncService>();
                if (sync == null)
                {
                    MessageBox.Show("Sync service is not registered.", "Sync", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show(
                    $"Starting sync...\n\nOnline: {sync.IsOnline}\nPending: {sync.PendingCount}",
                    "Sync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await sync.SyncPendingChangesAsync();

                MessageBox.Show(
                    $"Sync completed.\n\nPending now: {sync.PendingCount}",
                    "Sync",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Sync error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isSyncInProgress = false;
            }
        }

        private void Logout()
        {
            // Clear session
            SessionManager.Logout();

            // Close main window
            var mainWindow = System.Windows.Application.Current.MainWindow;
            mainWindow?.Close();

            // Show login window again
            var loginWindow = App.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();

            // Update app's main window reference
            System.Windows.Application.Current.MainWindow = loginWindow;
        }

        private void Exit()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
