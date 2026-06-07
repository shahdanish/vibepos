using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class PharmacyFormDialog : Window
    {
        private readonly PharmacyFormViewModel _viewModel;

        public PharmacyFormDialog(PharmacyFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseRequested += success => { DialogResult = success; };
        }

        public void LoadPharmacy(Pharmacy? pharmacy) => _viewModel.LoadPharmacy(pharmacy);

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
