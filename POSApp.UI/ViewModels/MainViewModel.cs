using System.Windows;
using System.Windows.Input;
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

        public Visibility SaleButtonVisibility =>
            PermissionManager.CanAccessSale(SessionManager.CurrentUser)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public event Action<SyncResult>? SyncResultReady;

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
        public ICommand OpenPurchaseReturnCommand { get; }
        public ICommand OpenSupplierManagementCommand { get; }
        public ICommand OpenPharmacyManagementCommand { get; }
        public ICommand OpenDoctorManagementCommand { get; }
        public ICommand OpenPharmacySaleCommand { get; }
        public ICommand OpenBackupRestoreCommand { get; }
        public ICommand SyncNowCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        public string CurrentUserInfo
        {
            get => _currentUserInfo;
            set => SetProperty(ref _currentUserInfo, value);
        }

        public string StoreTitle =>
            SessionManager.CurrentUser?.Role == "PharmacyUser"
                ? "Master Pharmaceuticals Distributor"
                : "Shah Jee Super Store";

        public System.Windows.Visibility DoctorButtonVisibility =>
            PermissionManager.CanManageDoctors(SessionManager.CurrentUser)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility PharmacyButtonVisibility =>
            PermissionManager.CanManagePharmacies(SessionManager.CurrentUser)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility PharmacySaleButtonVisibility =>
            PermissionManager.CanManagePharmacies(SessionManager.CurrentUser)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

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
            OpenPurchaseReturnCommand = new RelayCommand(_ => OpenPurchaseReturn());
            OpenSupplierManagementCommand = new RelayCommand(_ => OpenSupplierManagement());
            OpenPharmacyManagementCommand = new RelayCommand(_ => OpenPharmacyManagement());
            OpenDoctorManagementCommand = new RelayCommand(_ => OpenDoctorManagement());
            OpenPharmacySaleCommand = new RelayCommand(_ => OpenPharmacySale());
            OpenBackupRestoreCommand = new RelayCommand(_ => OpenBackupRestore());
            SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => !_isSyncInProgress);
            LogoutCommand = new RelayCommand(_ => Logout());
            ExitCommand = new RelayCommand(_ => Exit());
        }

        private void OpenSale()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access the sale screen.");
                return;
            }
            var saleWindow = App.Services?.GetRequiredService<SaleWindow>();
            saleWindow?.ShowDialog();
        }

        private void OpenWholeSale()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access the wholesale screen.");
                return;
            }
            var wholeSaleWindow = App.Services?.GetRequiredService<WholeSaleWindow>();
            wholeSaleWindow?.ShowDialog();
        }

        private void OpenSaleReturn()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access sale return.");
                return;
            }
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

        private void OpenPurchaseReturn()
        {
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases.");
                return;
            }
            var purchaseReturnWindow = App.Services?.GetRequiredService<PurchaseReturnWindow>();
            purchaseReturnWindow?.ShowDialog();
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

        private void OpenDoctorManagement()
        {
            if (!PermissionManager.CanManageDoctors(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to manage doctors.");
                return;
            }
            var doctorWindow = App.Services?.GetRequiredService<DoctorManagementWindow>();
            doctorWindow?.ShowDialog();
        }

        private void OpenPharmacySale()
        {
            if (!PermissionManager.CanManagePharmacies(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to access pharmacy sale.");
                return;
            }
            var window = App.Services?.GetRequiredService<PharmacySaleWindow>();
            window?.ShowDialog();
        }

        private void OpenPharmacyManagement()
        {
            if (!PermissionManager.CanManagePharmacies(SessionManager.CurrentUser))
            {
                NotificationHelper.ValidationErrorCustom("You don't have permission to manage pharmacies.");
                return;
            }
            var pharmacyWindow = App.Services?.GetRequiredService<PharmacyManagementWindow>();
            pharmacyWindow?.ShowDialog();
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

        private async Task SyncNowAsync()
        {
            if (_isSyncInProgress) return;
            _isSyncInProgress = true;
            try
            {
                var sync = App.Services?.GetRequiredService<ISyncService>();
                if (sync is null) return;
                var result = await sync.ResetAndForceSyncAsync();
                SyncResultReady?.Invoke(result);
            }
            catch (Exception ex)
            {
                SyncResultReady?.Invoke(new SyncResult { Success = false, ErrorMessage = ex.Message, Timestamp = DateTime.Now });
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
