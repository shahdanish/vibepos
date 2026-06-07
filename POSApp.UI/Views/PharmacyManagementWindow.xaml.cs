using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace POSApp.UI.Views
{
    public partial class PharmacyManagementWindow : Window
    {
        private readonly PharmacyManagementViewModel _viewModel;

        public PharmacyManagementWindow(PharmacyManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.OpenFormRequested += OpenFormDialog;
        }

        private void OpenFormDialog(Pharmacy? pharmacy)
        {
            var dialog = App.Services!.GetRequiredService<PharmacyFormDialog>();
            dialog.Owner = this;
            dialog.LoadPharmacy(pharmacy);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.LoadDataAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
