using System.Windows.Input;
using POSApp.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.Views;

namespace POSApp.UI.ViewModels
{
    public sealed class MainViewModel : ViewModelBase
    {
        private string _currentUserInfo = string.Empty;

        public ICommand OpenSaleCommand { get; }
        public ICommand OpenWholeSaleCommand { get; }
        public ICommand OpenSaleReturnCommand { get; }
        public ICommand OpenSalesReportCommand { get; }
        public ICommand OpenProductManagementCommand { get; }
        public ICommand OpenCategoryManagementCommand { get; }
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
