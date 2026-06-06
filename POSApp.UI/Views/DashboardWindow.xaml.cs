using System.Windows;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(DashboardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.SyncResultReady += OnSyncResultReady;
        }

        private void OnSyncResultReady(Core.Interfaces.SyncResult result)
        {
            if (result.WasSkipped)
            {
                SyncAlertDialog.ShowWarning(this, result.SkipReason!);
            }
            else if (result.Success)
            {
                SyncAlertDialog.ShowSuccess(this, result.PushedCount, result.Timestamp);
            }
            else
            {
                SyncAlertDialog.ShowError(this, result.PushedCount, result.FailedCount,
                    result.ErrorMessage ?? "Unknown error");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
