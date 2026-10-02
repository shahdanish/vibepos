using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
using POSApp.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.Views;

namespace POSApp.UI.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private string _currentUserInfo = string.Empty;

        // ── Visibility ────────────────────────────────────────────────────────

        public Visibility SaleButtonVisibility =>
            PermissionManager.CanAccessSale(SessionManager.CurrentUser)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PharmacyButtonVisibility =>
            PermissionManager.CanManagePharmacies(SessionManager.CurrentUser) && EditionGate.IsInBuild(AppFeature.Pharmacy)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PharmacySaleButtonVisibility =>
            SessionManager.HasPermission(Permissions.PharmacySale) && EditionGate.IsInBuild(AppFeature.Pharmacy)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DoctorButtonVisibility =>
            PermissionManager.CanManageDoctors(SessionManager.CurrentUser) && EditionGate.IsInBuild(AppFeature.Pharmacy)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CallScheduleButtonVisibility =>
            SessionManager.HasPermission(Permissions.CallScheduleManage) && EditionGate.IsInBuild(AppFeature.Pharmacy)
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>US shops, for users who can see sales reports.</summary>
        public Visibility TaxReportVisibility =>
            Region.IsUnitedStates && SessionManager.HasPermission(Permissions.ReportsSales)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility UserManagementVisibility =>
            SessionManager.HasPermission(Permissions.UsersManage)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmployeeButtonVisibility =>
            PermissionManager.CanManageEmployees(SessionManager.CurrentUser)
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Shop identity, currency and regional setup — admin-only.</summary>
        public Visibility BusinessSettingsVisibility =>
            SessionManager.HasPermission(Permissions.SystemSettings)
                ? Visibility.Visible : Visibility.Collapsed;

        // ── Edition (Store Lite / Pro) ────────────────────────────────────────

        /// <summary>"Upgrade to Pro" button in the header — only in the free Store tier.</summary>
        public Visibility UpgradeVisibility =>
            EditionGate.Edition == AppEdition.StoreLite ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Small edition badge next to the user name ("Lite" / "Pro"); hidden for the direct edition.</summary>
        public string EditionBadge => EditionGate.Edition switch
        {
            AppEdition.StoreLite => "Lite",
            AppEdition.StorePro  => "Pro",
            _ => string.Empty
        };

        public string? EditionBadgeTooltip =>
            EditionGate.Edition == AppEdition.StorePro && EditionGate.Service?.ProExpiresOn is { } until
                ? $"Swifttill Pro subscription — current period ends {Region.LongDate(until.LocalDateTime)}"
                : EditionGate.Edition == AppEdition.StoreLite ? "Swifttill Lite (free)" : null;

        public Visibility EditionBadgeVisibility =>
            EditionPolicy.IsStore(EditionGate.Edition) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand UpgradeCommand { get; }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand OpenSaleCommand { get; }
        public ICommand OpenWholeSaleCommand { get; }
        public ICommand OpenSaleReturnCommand { get; }
        public ICommand OpenSalesReportCommand { get; }
        public ICommand OpenTaxReportCommand { get; }
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
        public ICommand OpenCallScheduleCommand { get; }
        public ICommand OpenPharmacySaleCommand { get; }
        public ICommand OpenBackupRestoreCommand { get; }
        public ICommand OpenBusinessSettingsCommand { get; }
        public ICommand OpenEmployeeManagementCommand { get; }
        public ICommand OpenSalarySlipCommand { get; }
        public ICommand OpenUserManagementCommand { get; }
        public ICommand OpenRoleManagementCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand ExitCommand { get; }

        // ── Properties ────────────────────────────────────────────────────────

        public string CurrentUserInfo
        {
            get => _currentUserInfo;
            set => SetProperty(ref _currentUserInfo, value);
        }

        /// <summary>
        /// The shop name shown across the top of the main window. Comes from the shop's own
        /// Receipt Settings so one build can ship to any client.
        /// </summary>
        public string StoreTitle => ShopTitleExtension.Build(null);

        public DashboardViewModel DashboardViewModel { get; }

        public MainViewModel(DashboardViewModel dashboardViewModel)
        {
            DashboardViewModel = dashboardViewModel;

            if (SessionManager.CurrentUser != null)
                CurrentUserInfo = $"Logged in as: {SessionManager.CurrentUser.Username} ({SessionManager.CurrentUser.RoleName})";

            OpenSaleCommand             = new RelayCommand(_ => OpenSale());
            OpenWholeSaleCommand        = new RelayCommand(_ => OpenWholeSale());
            OpenSaleReturnCommand       = new RelayCommand(_ => OpenSaleReturn());
            OpenSalesReportCommand      = new RelayCommand(_ => OpenSalesReport());
            OpenTaxReportCommand        = new RelayCommand(_ => OpenTaxReport());
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
            OpenCallScheduleCommand     = new RelayCommand(_ => OpenCallSchedule());
            OpenPharmacySaleCommand     = new RelayCommand(_ => OpenPharmacySale());
            OpenBackupRestoreCommand    = new RelayCommand(_ => OpenBackupRestore());
            OpenBusinessSettingsCommand  = new RelayCommand(_ => OpenBusinessSettings());
            OpenEmployeeManagementCommand = new RelayCommand(_ => OpenEmployeeManagement());
            OpenSalarySlipCommand       = new RelayCommand(_ => OpenSalarySlip());
            OpenUserManagementCommand   = new RelayCommand(_ => OpenUserManagement());
            OpenRoleManagementCommand   = new RelayCommand(_ => OpenRoleManagement());
            UpgradeCommand              = new RelayCommand(_ => EditionGate.ShowUpgrade());
            LogoutCommand               = new RelayCommand(_ => Logout());
            ExitCommand                 = new RelayCommand(_ => Exit());

            if (EditionGate.Service is { } edition)
                edition.EditionChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(UpgradeVisibility));
                    OnPropertyChanged(nameof(EditionBadge));
                    OnPropertyChanged(nameof(EditionBadgeVisibility));
                    OnPropertyChanged(nameof(EditionBadgeTooltip));
                });
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
            if (!EditionGate.Require(AppFeature.Wholesale)) return;
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

        private void OpenTaxReport()
        {
            if (!SessionManager.HasPermission(Permissions.ReportsSales))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to view sales reports."); return; }
            var dialog = App.Services?.GetRequiredService<TaxReportDialog>();
            if (dialog == null) return;
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        }

        private void OpenProductManagement()
            => App.Services?.GetRequiredService<ProductManagementWindow>().ShowDialog();

        private void OpenCategoryManagement()
            => App.Services?.GetRequiredService<CategoryManagementWindow>().ShowDialog();

        private void OpenDashboard()
            => DashboardViewModel.RefreshCommand.Execute(null);

        private void OpenExpense()
        {
            if (!EditionGate.Require(AppFeature.Expenses)) return;
            App.Services?.GetRequiredService<ExpenseWindow>().ShowDialog();
        }

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
            if (!EditionGate.Require(AppFeature.Purchases)) return;
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases."); return; }
            App.Services?.GetRequiredService<PurchaseEntryWindow>().ShowDialog();
        }

        private void OpenPurchaseReturn()
        {
            if (!EditionGate.Require(AppFeature.Purchases)) return;
            if (!PermissionManager.CanManagePurchases(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage purchases."); return; }
            App.Services?.GetRequiredService<PurchaseReturnWindow>().ShowDialog();
        }

        private void OpenSupplierManagement()
        {
            if (!EditionGate.Require(AppFeature.Purchases)) return;
            if (!PermissionManager.CanManageSuppliers(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage suppliers."); return; }
            App.Services?.GetRequiredService<SupplierManagementWindow>().ShowDialog();
        }

        private void OpenDoctorManagement()
        {
            if (!EditionGate.Require(AppFeature.Pharmacy)) return;
            if (!PermissionManager.CanManageDoctors(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage doctors."); return; }
            App.Services?.GetRequiredService<DoctorManagementWindow>().ShowDialog();
        }

        private void OpenCallSchedule()
        {
            if (!EditionGate.Require(AppFeature.Pharmacy)) return;
            if (!SessionManager.HasPermission(Permissions.CallScheduleManage))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access call scheduling."); return; }
            App.Services?.GetRequiredService<CallScheduleWindow>().ShowDialog();
        }

        private void OpenPharmacySale()
        {
            if (!EditionGate.Require(AppFeature.Pharmacy)) return;
            if (!SessionManager.HasPermission(Permissions.PharmacySale))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to access pharmacy sale."); return; }
            App.Services?.GetRequiredService<PharmacySaleWindow>().ShowDialog();
        }

        private void OpenPharmacyManagement()
        {
            if (!EditionGate.Require(AppFeature.Pharmacy)) return;
            if (!PermissionManager.CanManagePharmacies(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage pharmacies."); return; }
            App.Services?.GetRequiredService<PharmacyManagementWindow>().ShowDialog();
        }

        private void OpenEmployeeManagement()
        {
            if (!EditionGate.Require(AppFeature.HumanResources)) return;
            if (!PermissionManager.CanManageEmployees(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage employees."); return; }
            App.Services?.GetRequiredService<EmployeeManagementWindow>().ShowDialog();
        }

        private void OpenSalarySlip()
        {
            if (!EditionGate.Require(AppFeature.HumanResources)) return;
            if (!PermissionManager.CanManageSalary(SessionManager.CurrentUser))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage salary slips."); return; }
            App.Services?.GetRequiredService<SalarySlipWindow>().ShowDialog();
        }

        private void OpenBusinessSettings()
        {
            if (!SessionManager.HasPermission(Permissions.SystemSettings))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to change receipt settings."); return; }
            new BusinessSettingsWindow { Owner = Application.Current.MainWindow }.ShowDialog();
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
            if (!EditionGate.Require(AppFeature.RoleManagement)) return;
            if (!SessionManager.HasPermission(Permissions.UsersManage))
            { NotificationHelper.ValidationErrorCustom("You don't have permission to manage roles."); return; }
            App.Services?.GetRequiredService<RoleManagementWindow>().ShowDialog();
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
