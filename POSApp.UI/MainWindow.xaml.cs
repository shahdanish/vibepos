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
        viewModel.SyncResultReady += OnSyncResultReady;
        viewModel.DashboardViewModel.SyncResultReady += OnSyncResultReady;
    }

    private void OnSyncResultReady(SyncResult result)
    {
        if (result.WasSkipped)
            SyncAlertDialog.ShowWarning(this, result.SkipReason!);
        else if (result.Success)
            SyncAlertDialog.ShowSuccess(this, result.PushedCount, result.Timestamp);
        else
            SyncAlertDialog.ShowError(this, result.PushedCount, result.FailedCount, result.ErrorMessage ?? "Unknown error");
    }
}
