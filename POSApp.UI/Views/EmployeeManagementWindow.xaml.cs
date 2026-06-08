using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace POSApp.UI.Views
{
    public partial class EmployeeManagementWindow : Window
    {
        private readonly EmployeeManagementViewModel _viewModel;

        public EmployeeManagementWindow(EmployeeManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.OpenFormRequested += OpenFormDialog;
            _viewModel.GenerateSalarySlipRequested += OpenSalarySlipForEmployee;
        }

        private void OpenFormDialog(Employee? employee)
        {
            var dialog = App.Services!.GetRequiredService<EmployeeFormDialog>();
            dialog.Owner = this;
            _ = dialog.LoadEmployeeAsync(employee);
            if (dialog.ShowDialog() == true)
                _ = _viewModel.LoadDataAsync();
        }

        private void OpenSalarySlipForEmployee(Employee employee)
        {
            var window = App.Services!.GetRequiredService<SalarySlipWindow>();
            window.Owner = this;
            window.PreSelectEmployee(employee);
            window.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
