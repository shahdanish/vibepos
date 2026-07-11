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
            viewModel.CloudResultReady += OnCloudResultReady;
        }

        private void OnCloudResultReady(Core.Interfaces.CloudBackupResult result)
        {
            if (result.WasSkipped)
            {
                SyncAlertDialog.ShowWarning(this, result.SkipReason!);
            }
            else if (result.Success && result.IsRestore)
            {
                SyncAlertDialog.ShowCloudRestoreSuccess(this, result.SnapshotTime ?? DateTime.Now, result.SizeBytes);
            }
            else if (result.Success)
            {
                SyncAlertDialog.ShowCloudBackupSuccess(this, result.SizeBytes, result.SnapshotTime ?? DateTime.Now, result.ChunkCount);
            }
            else
            {
                SyncAlertDialog.ShowCloudError(this, result.IsRestore, result.ErrorMessage ?? "Unknown error");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
