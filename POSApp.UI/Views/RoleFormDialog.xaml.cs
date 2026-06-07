using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class RoleFormDialog : Window
    {
        private readonly RoleFormViewModel _viewModel;

        public RoleFormDialog(RoleFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseRequested += success => { DialogResult = success; };
        }

        public async Task LoadRoleAsync(Role? role) => await _viewModel.LoadRoleAsync(role);

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
