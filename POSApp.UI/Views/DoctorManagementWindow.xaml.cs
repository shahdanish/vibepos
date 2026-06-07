using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace POSApp.UI.Views
{
    public partial class DoctorManagementWindow : Window
    {
        private readonly DoctorManagementViewModel _viewModel;

        public DoctorManagementWindow(DoctorManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.OpenFormRequested += OpenFormDialog;
        }

        private void OpenFormDialog(Doctor? doctor)
        {
            var dialog = App.Services!.GetRequiredService<DoctorFormDialog>();
            dialog.Owner = this;
            dialog.LoadDoctor(doctor);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.LoadDataAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
