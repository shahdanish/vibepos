using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.UI.Views;

namespace POSApp.UI.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly IUserRepository _userRepository;
        
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isLoginEnabled = true;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsLoginEnabled
        {
            get => _isLoginEnabled;
            set => SetProperty(ref _isLoginEnabled, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand CancelCommand { get; }

        public LoginViewModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;
            
            LoginCommand = new RelayCommand(async _ => await ExecuteLogin(), _ => CanExecuteLogin());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        private bool CanExecuteLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) && 
                   !string.IsNullOrWhiteSpace(Password) && 
                   IsLoginEnabled;
        }

        private async Task ExecuteLogin()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }
        
            try
            {
                IsLoginEnabled = false;
                ErrorMessage = string.Empty;
        
                // Validate user credentials
                var user = await _userRepository.ValidateUserAsync(Username, Password);
        
                if (user != null)
                {
                    // Login successful
                    SessionManager.CurrentUser = user;
                                    
                    // Update last login date
                    user.LastLoginDate = DateTime.Now;
                    await _userRepository.UpdateAsync(user);
                
                    // Use Dispatcher to ensure we're on UI thread
                    await Application.Current.Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            // Hide THIS window first
                            var thisWindow = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.DataContext == this);
                                                        
                            if (thisWindow != null)
                            {
                                thisWindow.Hide();
                            }
                            else
                            {
                                return;
                            }
                                            
                            await Task.Delay(100);
                                            
                            // Create MainViewModel
                            var mainViewModel = new MainViewModel();
                                            
                            // Create MainWindow
                            var mainWindow = new MainWindow(mainViewModel);
                                            
                            // Show it
                            Application.Current.MainWindow = mainWindow;
                            mainWindow.Show();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error in window transition:\n\n{ex.Message}", 
                                "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                else
                {
                    // Login failed - INVALID CREDENTIALS
                    ErrorMessage = "❌ Invalid username or password. Please try again.";
                    
                    // Show MessageBox with error
                    MessageBox.Show(ErrorMessage, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
                MessageBox.Show(ErrorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoginEnabled = true;
            }
        }

        private void ExecuteCancel()
        {
            Application.Current.Shutdown();
        }
    }
}
