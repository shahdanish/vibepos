using System.Windows;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class PurchaseReturnWindow : Window
    {
        public PurchaseReturnWindow(PurchaseReturnViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
