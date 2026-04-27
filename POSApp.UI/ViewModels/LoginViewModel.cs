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
            // Write to log file immediately
            var logFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "POS_Login_Log.txt");
            try
            {
                using (var writer = new System.IO.StreamWriter(logFile, true))
                {
                    await writer.WriteLineAsync($"\n=== LOGIN ATTEMPT START: {DateTime.Now} ===");
                    await writer.WriteLineAsync($"Username: '{Username}', Password: '{Password}'");
                    await writer.FlushAsync();
                }
            }
            catch { /* Ignore log errors */ }
                    
            System.Diagnostics.Debug.WriteLine($"=== LOGIN ATTEMPT ===");
            System.Diagnostics.Debug.WriteLine($"Username: '{Username}', Password: '{Password}'");
                    
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                System.Diagnostics.Debug.WriteLine($"Error: Empty credentials");
                return;
            }
        
            try
            {
                IsLoginEnabled = false;
                ErrorMessage = string.Empty;
        
                System.Diagnostics.Debug.WriteLine($"Calling ValidateUserAsync...");
                using (var writer = new System.IO.StreamWriter(logFile, true))
                {
                    await writer.WriteLineAsync($"Calling ValidateUserAsync for user: {Username}");
                    await writer.FlushAsync();
                }
                        
                // Validate user credentials
                var user = await _userRepository.ValidateUserAsync(Username, Password);
        
                System.Diagnostics.Debug.WriteLine(user != null 
                    ? $"✅ User found: {user.Username}, Role: {user.Role}" 
                    : $"❌ User not found or invalid credentials");
                            
                using (var writer = new System.IO.StreamWriter(logFile, true))
                {
                    if (user != null)
                        await writer.WriteLineAsync($"✅ SUCCESS: User authenticated - {user.Username} ({user.Role})");
                    else
                        await writer.WriteLineAsync($"❌ FAILED: Invalid credentials");
                    await writer.FlushAsync();
                }
        
                if (user != null)
                {
                    // Login successful
                    SessionManager.CurrentUser = user;
                    System.Diagnostics.Debug.WriteLine($"✅ User authenticated: {user.Username}");
                                    
                    using (var writer = new System.IO.StreamWriter(logFile, true))
                    {
                        await writer.WriteLineAsync($"User authenticated: {user.Username} ({user.Role})");
                        await writer.WriteLineAsync($"Proceeding with window transition...");
                        await writer.FlushAsync();
                    }
                                    
                    // Update last login date
                    user.LastLoginDate = DateTime.Now;
                    await _userRepository.UpdateAsync(user);
                    System.Diagnostics.Debug.WriteLine($"Last login date updated");
                
                    // Use Dispatcher to ensure we're on UI thread
                    await Application.Current.Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Dispatcher invoked");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"=== DISPATCHER STARTED ===");
                                await writer.FlushAsync();
                            }
                                            
                            // Hide THIS window first
                            var thisWindow = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.DataContext == this);
                                                        
                            if (thisWindow != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found login window: {thisWindow.GetType().Name}");
                                using (var writer = new System.IO.StreamWriter(logFile, true))
                                {
                                    await writer.WriteLineAsync($"Found login window: {thisWindow.GetType().Name}");
                                    await writer.WriteLineAsync($"Hiding login window...");
                                    await writer.FlushAsync();
                                }
                                                            
                                thisWindow.Hide();
                                System.Diagnostics.Debug.WriteLine($"Login window hidden");
                                                                
                                // Proceed to create main window (no debug message)
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"ERROR: Could not find login window!");
                                using (var writer = new System.IO.StreamWriter(logFile, true))
                                {
                                    await writer.WriteLineAsync($"ERROR: Login window not found by DataContext!");
                                    await writer.FlushAsync();
                                }
                                return;
                            }
                                            
                            await Task.Delay(100);
                                            
                            // Create MainViewModel
                            System.Diagnostics.Debug.WriteLine($"Creating MainViewModel...");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"Step 1: Creating MainViewModel...");
                                await writer.FlushAsync();
                            }
                                            
                            var mainViewModel = new MainViewModel();
                            System.Diagnostics.Debug.WriteLine($"✅ MainViewModel created");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"✅ Step 1 COMPLETE");
                                await writer.FlushAsync();
                            }
                                            
                            // Create MainWindow
                            System.Diagnostics.Debug.WriteLine($"Creating MainWindow...");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"Step 2: Creating MainWindow...");
                                await writer.FlushAsync();
                            }
                                            
                            var mainWindow = new MainWindow(mainViewModel);
                            System.Diagnostics.Debug.WriteLine($"✅ MainWindow created");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"✅ Step 2 COMPLETE");
                                await writer.FlushAsync();
                            }
                                            
                            // Show it
                            Application.Current.MainWindow = mainWindow;
                            mainWindow.Show();
                                            
                            System.Diagnostics.Debug.WriteLine($"✅ MainWindow shown! Windows count: {Application.Current.Windows.Count}");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"✅ Step 3: MainWindow.Show() called");
                                await writer.WriteLineAsync($"Active windows: {Application.Current.Windows.Count}");
                                await writer.WriteLineAsync($"=== LOGIN SUCCESSFUL ===\n");
                                await writer.FlushAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌❌❌ DISPATCHER ERROR: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                            using (var writer = new System.IO.StreamWriter(logFile, true))
                            {
                                await writer.WriteLineAsync($"❌❌❌ DISPATCHER EXCEPTION: {ex.Message}");
                                await writer.WriteLineAsync($"Stack: {ex.StackTrace}");
                                if (ex.InnerException != null)
                                    await writer.WriteLineAsync($"Inner: {ex.InnerException.Message}");
                                await writer.WriteLineAsync($"=== DISPATCHER FAILED ===\n");
                                await writer.FlushAsync();
                            }
                            MessageBox.Show($"Error in window transition:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                                "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                else
                {
                    // Login failed - INVALID CREDENTIALS
                    ErrorMessage = "❌ Invalid username or password. Please try again.";
                    System.Diagnostics.Debug.WriteLine($"❌ Login failed - {ErrorMessage}");
                    using (var writer = new System.IO.StreamWriter(logFile, true))
                    {
                        await writer.WriteLineAsync($"❌ FAILED: Invalid credentials");
                        await writer.WriteLineAsync($"Error message shown to user: {ErrorMessage}");
                        await writer.FlushAsync();
                    }
                    
                    // Show MessageBox with error
                    MessageBox.Show(ErrorMessage, "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Exception during login: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                ErrorMessage = $"An error occurred: {ex.Message}";
                        
                using (var writer = new System.IO.StreamWriter(logFile, true))
                {
                    await writer.WriteLineAsync($"❌ EXCEPTION: {ex.Message}");
                    await writer.WriteLineAsync($"Stack: {ex.StackTrace}");
                    await writer.WriteLineAsync($"=== LOGIN PROCESS EXCEPTION ===\n");
                    await writer.FlushAsync();
                }
            }
            finally
            {
                IsLoginEnabled = true;
                System.Diagnostics.Debug.WriteLine($"=== Login process completed ===");
                        
                using (var writer = new System.IO.StreamWriter(logFile, true))
                {
                    await writer.WriteLineAsync($"Process completed at {DateTime.Now}");
                    await writer.WriteLineAsync($"========================================\n");
                    await writer.FlushAsync();
                }
            }
        }

        private void ExecuteCancel()
        {
            Application.Current.Shutdown();
        }
    }
}
