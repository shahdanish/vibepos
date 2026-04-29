using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();

            // Get ViewModel from DI container
            _viewModel = App.Services.GetRequiredService<LoginViewModel>();
            DataContext = _viewModel;
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                // Bind Password property manually
                _viewModel.Password = passwordBox.Password;
            }
        }

        private void TxtUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                txtPassword.Focus();
            }
        }

        private async void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                // Trigger login
                if (_viewModel.LoginCommand.CanExecute(null))
                {
                    await Task.Delay(100); // Small delay to ensure password is bound
                    _viewModel.LoginCommand.Execute(null);
                }
            }
        }
    }
}
