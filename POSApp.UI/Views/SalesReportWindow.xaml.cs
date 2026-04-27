using System.Windows;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SalesReportWindow : Window
    {
        public SalesReportWindow(SalesReportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
