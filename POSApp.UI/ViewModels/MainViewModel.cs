using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
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

        public event Action<SyncResult>? SyncResultReady;

        // ── Visibility ────────────────────────────────────────────────────────

        public Visibility SaleButtonVisibility =>
            PermissionManager.CanAccessSale(SessionManager.CurrentUser)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PharmacyButtonVisibility =>
            PermissionManager.CanManagePharmacies(SessionManager.CurrentUser)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PharmacySaleButtonVisibility =>
            SessionManager.HasPermission(Permissions.PharmacySale)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DoctorButtonVisibility =>
            PermissionManager.CanManageDoctors(SessionManager.CurrentUser)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility UserManagementVisibility =>
            SessionManager.HasPermission(Permissions.UsersManage)
                ? Visibility.Visible : Visibility.Collapsed;

        // ── Commands ──────────────────────────────────────────────────────────

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
        public ICommand OpenUserManagementCommand { get; }
        public ICommand OpenRoleManagementCommand { get; }
        public ICommand SyncNowCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        // ── Properties ────────────────────────────────────────────────────────

        public string CurrentUserInfo
        {
            get => _currentUserInfo;
            set => SetProperty(ref _currentUserInfo, value);
        }

        public string StoreTitle =>
            SessionManager.HasPermission(Permissions.PharmacySale)
                ? "Master Pharmaceuticals Distributor"
                : "Shah Jee Super Store";

        public MainViewModel()
        {
            if (SessionManager.CurrentUser != null)
                CurrentUserInfo = $"Logged in as: {SessionManager.CurrentUser.Username} ({SessionManager.CurrentUser.RoleName})";

            OpenSaleCommand             = new RelayCommand(_ => OpenSale());
            OpenWholeSaleCommand        = new RelayCommand(_ => OpenWholeSale());
            OpenSaleReturnCommand       = new RelayCommand(_ => OpenSaleReturn());
            OpenSalesReportCommand      = new RelayCommand(_ => OpenSalesReport());
            OpenProductManagementCommand = new RelayCommand(_ => OpenProductManagement());
            OpenCategoryManagementCommand = new RelayCommand(_ => OpenCategoryManagement());
            OpenDashboardCommand        = new RelayCommand(_ => OpenDashboard());
            OpenExpenseCommand          = new RelayCommand(_ => OpenExpense());
            OpenShiftCommand            = new RelayCommand(_ => OpenShift());
            OpenCustomerLedgerCommand   = new RelayCommand(_ => OpenCustomerLedger());
            OpenDailySummaryCommand     = new RelayCommand(_ => OpenDailySummary());
            OpenPurchaseEntryCommand    = new RelayCommand(_ => OpenPurchaseEntry());
            OpenPurchaseReturnCommand   = new RelayCommand(_ => OpenPurchaseReturn());
            OpenSupplierManagementCommand = new RelayCommand(_ => OpenSupplierManagement());
            OpenPharmacyManagementCommand = new RelayCommand(_ => OpenPharmacyManagement());
            OpenDoctorManagementCommand = new RelayCommand(_ => OpenDoctorManagement());
            OpenPharmacySaleCommand     = new RelayCommand(_ => OpenPharmacySale());
            OpenBackupRestoreCommand    = new RelayCommand(_ => OpenBackupRestore());
            OpenUserManagementCommand   = new RelayCommand(_ => OpenUserManagement());
            OpenRoleManagementCommand   = new RelayCommand(_ => OpenRoleManagement());
            SyncNowCommand              = new AsyncRelayCommand(SyncNowAsync, () => !_isSyncInProgress);
            LogoutCommand               = new RelayCommand(_ => Logout());
            ExitCommand                 = new RelayCommand(_ => Exit());
        }

        // ── Navigation handlers ───────────────────────────────────────────────

        private void OpenSale()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access the sale screen."); return; }
            App.Services?.GetRequiredService<SaleWindow>().ShowDialog();
        }

        private void OpenWholeSale()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access the wholesale screen."); return; }
            App.Services?.GetRequiredService<WholeSaleWindow>().ShowDialog();
        }

        private void OpenSaleReturn()
        {
            if (!PermissionManager.CanAccessSale(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access sale return."); return; }
            App.Services?.GetRequiredService<SaleReturnWindow>().ShowDialog();
        }

        private void OpenSalesReport()
            => App.Services?.GetRequiredService<SalesReportWindow>().ShowDialog();

        private void OpenProductManagement()
            => App.Services?.GetRequiredService<ProductManagementWindow>().ShowDialog();

        private void OpenCategoryManagement()
            => App.Services?.GetRequiredService<CategoryManagementWindow>().ShowDialog();

        private void OpenDashboard()
            => App.Services?.GetRequiredService<DashboardWindow>().ShowDialog();

        private void OpenExpense()
            => App.Services?.GetRequiredService<ExpenseWindow>().ShowDialog();

        private void OpenShift()
            => App.Services?.GetRequiredService<ShiftWindow>().ShowDialog();

        private void OpenCustomerLedger()
            => App.Services?.GetRequiredService<CustomerLedgerWindow>().ShowDialog();

        private void OpenDailySummary()
        {
            if (!PermissionManager.CanAccessDailySummary(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access daily summary."); return; }
            App.Services?.GetRequiredService<DailySummaryWindow>().ShowDialog();
        }

        private void OpenPurchaseEntry()
        {
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases."); return; }
            App.Services?.GetRequiredService<PurchaseEntryWindow>().ShowDialog();
        }

        private void OpenPurchaseReturn()
        {
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases."); return; }
            App.Services?.GetRequiredService<PurchaseReturnWindow>().ShowDialog();
        }

        private void OpenSupplierManagement()
        {
            if (!PermissionManager.CanManageSuppliers(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage suppliers."); return; }
            App.Services?.GetRequiredService<SupplierManagementWindow>().ShowDialog();
        }

        private void OpenDoctorManagement()
        {
            if (!PermissionManager.CanManageDoctors(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage doctors."); return; }
            App.Services?.GetRequiredService<DoctorManagementWindow>().ShowDialog();
        }

        private void OpenPharmacySale()
        {
            if (!SessionManager.HasPermission(Permissions.PharmacySale))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access pharmacy sale."); return; }
            App.Services?.GetRequiredService<PharmacySaleWindow>().ShowDialog();
        }

        private void OpenPharmacyManagement()
        {
            if (!PermissionManager.CanManagePharmacies(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage pharmacies."); return; }
            App.Services?.GetRequiredService<PharmacyManagementWindow>().ShowDialog();
        }

        private void OpenBackupRestore()
        {
            if (!SessionManager.HasPermission(Permissions.BackupAccess))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access backup/restore."); return; }
            App.Services?.GetRequiredService<BackupRestoreWindow>().ShowDialog();
        }

        private void OpenUserManagement()
        {
            if (!SessionManager.HasPermission(Permissions.UsersManage))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage users."); return; }
            App.Services?.GetRequiredService<UserManagementWindow>().ShowDialog();
        }

        private void OpenRoleManagement()
        {
            if (!SessionManager.HasPermission(Permissions.UsersManage))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage roles."); return; }
            App.Services?.GetRequiredService<RoleManagementWindow>().ShowDialog();
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
            SessionManager.Logout();
            var mainWindow = Application.Current.MainWindow;
            mainWindow?.Close();
            var loginWindow = App.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Application.Current.MainWindow = loginWindow;
        }

        private void Exit() => Application.Current.Shutdown();
    }
}
