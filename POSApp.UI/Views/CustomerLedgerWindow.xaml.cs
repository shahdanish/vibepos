using System.Windows;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class CustomerLedgerWindow : Window
    {
        public CustomerLedgerWindow(CustomerLedgerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // "Khata" in Pakistan, "Charge Accounts" elsewhere (see Region).
            Title = ShopTitleExtension.Build(Region.CustomerAccountsTitle);
            HeadingText.Text = "📒 " + Region.CustomerAccountsTitle;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
