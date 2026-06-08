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
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
