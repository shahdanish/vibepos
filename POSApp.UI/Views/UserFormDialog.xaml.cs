using System.Windows;
using POSApp.Core.Entities;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class UserFormDialog : Window
    {
        private readonly UserFormViewModel _viewModel;

        public UserFormDialog(UserFormViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.CloseRequested += success => { DialogResult = success; };
        }

        public async Task LoadUserAsync(User? user)
        {
            await _viewModel.LoadUserAsync(user);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Pass password values from PasswordBox (can't data-bind for security)
            _viewModel.NewPassword = PasswordBox.Password;
            _viewModel.ConfirmPassword = ConfirmPasswordBox.Password;

            if (_viewModel.SaveCommand.CanExecute(null))
                _viewModel.SaveCommand.Execute(null);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
