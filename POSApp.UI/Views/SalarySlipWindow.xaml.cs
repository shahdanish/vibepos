using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SalarySlipWindow : Window
    {
        private readonly SalarySlipViewModel _viewModel;

        public SalarySlipWindow(SalarySlipViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        public void PreSelectEmployee(Employee employee)
        {
            // Wait until employees are loaded, then select
            Loaded += (_, _) =>
            {
                var match = _viewModel.Employees.FirstOrDefault(e => e.Id == employee.Id);
                if (match != null)
                    _viewModel.SelectedEmployee = match;
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
