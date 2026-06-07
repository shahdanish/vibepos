using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace POSApp.UI.Views
{
    public partial class RoleManagementWindow : Window
    {
        private readonly RoleManagementViewModel _viewModel;

        public RoleManagementWindow(RoleManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.OpenFormRequested += OpenFormDialog;
        }

        private void OpenFormDialog(Role? role)
        {
            var dialog = App.Services!.GetRequiredService<RoleFormDialog>();
            dialog.Owner = this;
            _ = dialog.LoadRoleAsync(role);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.LoadDataAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
