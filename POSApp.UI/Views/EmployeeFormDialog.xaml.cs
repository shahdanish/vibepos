using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class EmployeeFormDialog : Window
    {
        private readonly EmployeeFormViewModel _viewModel;

        public EmployeeFormDialog(EmployeeFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestClose += result => DialogResult = result;
        }

        public async Task LoadEmployeeAsync(Employee? employee)
        {
            await _viewModel.LoadEmployeeAsync(employee);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
