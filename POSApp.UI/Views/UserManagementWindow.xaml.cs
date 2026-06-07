using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace POSApp.UI.Views
{
    public partial class UserManagementWindow : Window
    {
        private readonly UserManagementViewModel _viewModel;

        public UserManagementWindow(UserManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.OpenFormRequested += OpenFormDialog;
        }

        private void OpenFormDialog(User? user)
        {
            var dialog = App.Services!.GetRequiredService<UserFormDialog>();
            dialog.Owner = this;
            _ = dialog.LoadUserAsync(user);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.LoadDataAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
