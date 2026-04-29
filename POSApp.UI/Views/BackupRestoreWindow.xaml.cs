using System.Windows;

namespace POSApp.UI.Views
{
    public partial class BackupRestoreWindow : Window
    {
        public BackupRestoreWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
