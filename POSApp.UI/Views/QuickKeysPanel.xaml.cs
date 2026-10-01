using System.Windows;
using System.Windows.Controls;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    /// <summary>The quick-key tiles shown beside the cart on the Sale and Wholesale screens.</summary>
    public partial class QuickKeysPanel : UserControl
    {
        public QuickKeysPanel()
        {
            InitializeComponent();
        }

        private async void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SaleViewModel vm) return;

            var dialog = new ManageQuickKeysDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
                await vm.LoadQuickKeysAsync();
        }
    }
}
