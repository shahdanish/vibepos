using System.Windows;

namespace POSApp.UI.Views
{
    public partial class DailySummaryWindow : Window
    {
        public DailySummaryWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
