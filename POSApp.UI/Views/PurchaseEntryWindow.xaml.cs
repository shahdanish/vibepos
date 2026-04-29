using System.Windows;

namespace POSApp.UI.Views
{
    public partial class PurchaseEntryWindow : Window
    {
        public PurchaseEntryWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
