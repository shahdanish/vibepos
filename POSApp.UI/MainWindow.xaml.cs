using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;

namespace POSApp.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.DashboardViewModel.CloudResultReady += OnCloudResultReady;
    }

    private void OnCloudResultReady(CloudBackupResult result)
    {
        if (result.WasSkipped)
            SyncAlertDialog.ShowWarning(this, result.SkipReason!);
        else if (result.Success && result.IsRestore)
            SyncAlertDialog.ShowCloudRestoreSuccess(this, result.SnapshotTime ?? DateTime.Now, result.SizeBytes);
        else if (result.Success)
            SyncAlertDialog.ShowCloudBackupSuccess(this, result.SizeBytes, result.SnapshotTime ?? DateTime.Now, result.ChunkCount);
        else
            SyncAlertDialog.ShowCloudError(this, result.IsRestore, result.ErrorMessage ?? "Unknown error");
    }
}
