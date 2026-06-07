using System.Windows;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class ProductManagementWindow : Window
    {
        public ProductManagementWindow(ProductManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.OnProductAdded = () =>
            {
                BarcodeBox.Clear();
                BarcodeBox.Focus();
            };

            // DataGridColumn is not a FrameworkElement so visibility must be set in code-behind
            var isPharmacy = SessionManager.CurrentUser?.Role == "PharmacyUser";
            BatchColumn.Visibility = isPharmacy ? Visibility.Visible : Visibility.Collapsed;
            ExpiryColumn.Visibility = isPharmacy ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BarcodeBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var vm = (ProductManagementViewModel)DataContext;
            await vm.ValidateBarcodeAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
