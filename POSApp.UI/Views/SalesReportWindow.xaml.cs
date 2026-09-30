using System.Windows;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SalesReportWindow : Window
    {
        private readonly SalesReportViewModel _vm;

        public SalesReportWindow(SalesReportViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
            Loaded += async (_, _) => await _vm.LoadAsync();

            // DataGrid columns aren't in the visual tree, so toggle them from here.
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SalesReportViewModel.ShowPharmacyColumns))
                    ApplyPharmacyColumns();
            };
            ApplyPharmacyColumns();
        }

        private void ApplyPharmacyColumns()
        {
            var pharmacy = _vm.ShowPharmacyColumns;
            CustomerColumn.Header = pharmacy ? "Customer / Pharmacy" : "Customer";
            DoctorColumn.Visibility = pharmacy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
