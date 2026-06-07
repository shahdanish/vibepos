using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class UserFormViewModel : ViewModelBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;

        private User? _editingUser;
        private string _username = string.Empty;
        private string _formTitle = "Add User";
        private Role? _selectedRole;
        private bool _isActive = true;
        private bool _isEditMode;

        public ObservableCollection<Role> Roles { get; } = new();

        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public Role? SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool IsEditMode => _isEditMode;

        public ICommand SaveCommand { get; }
        public event Action<bool>? CloseRequested;

        public UserFormViewModel(IUserRepository userRepository, IRoleRepository roleRepository)
        {
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            _ = LoadRolesAsync();
        }

        private async Task LoadRolesAsync()
        {
            var roles = await _roleRepository.GetAllAsync();
            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);
        }

        public async Task LoadUserAsync(User? user)
        {
            await LoadRolesAsync();

            if (user == null)
            {
                _editingUser = null;
                _isEditMode = false;
                FormTitle = "Add User";
                Username = string.Empty;
                IsActive = true;
                SelectedRole = Roles.FirstOrDefault(r => r.Name == "Cashier");
            }
            else
            {
                _editingUser = user;
                _isEditMode = true;
                FormTitle = $"Edit User — {user.Username}";
                Username = user.Username;
                IsActive = user.IsActive;
                SelectedRole = Roles.FirstOrDefault(r => r.Id == user.RoleId);
            }
        }

        // Called from code-behind with PasswordBox values
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                NotificationHelper.ValidationErrorCustom("Username is required.");
                return;
            }

            if (SelectedRole == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a role.");
                return;
            }

            // Password validation
            if (!_isEditMode)
            {
                if (string.IsNullOrWhiteSpace(NewPassword))
                {
                    NotificationHelper.ValidationErrorCustom("Password is required for new users.");
                    return;
                }
                if (NewPassword != ConfirmPassword)
                {
                    NotificationHelper.ValidationErrorCustom("Passwords do not match.");
                    return;
                }
            }
            else if (!string.IsNullOrEmpty(NewPassword) && NewPassword != ConfirmPassword)
            {
                NotificationHelper.ValidationErrorCustom("Passwords do not match.");
                return;
            }

            try
            {
                if (_isEditMode && _editingUser != null)
                {
                    // Check username uniqueness (exclude self)
                    if (!await _userRepository.IsUsernameUniqueAsync(Username, _editingUser.Id))
                    {
                        NotificationHelper.ValidationErrorCustom($"Username '{Username}' is already taken.");
                        return;
                    }

                    _editingUser.Username = Username.Trim();
                    _editingUser.RoleId = SelectedRole.Id;
                    _editingUser.IsActive = IsActive;

                    if (!string.IsNullOrEmpty(NewPassword))
                        _editingUser.PasswordHash = NewPassword;

                    await _userRepository.UpdateAsync(_editingUser);
                    NotificationHelper.ShowSuccess($"User '{Username}' updated successfully.");
                }
                else
                {
                    if (!await _userRepository.IsUsernameUniqueAsync(Username))
                    {
                        NotificationHelper.ValidationErrorCustom($"Username '{Username}' is already taken.");
                        return;
                    }

                    var newUser = new User
                    {
                        Username = Username.Trim(),
                        PasswordHash = NewPassword!,
                        RoleId = SelectedRole.Id,
                        IsActive = IsActive,
                        CreatedDate = DateTime.Now
                    };
                    await _userRepository.CreateAsync(newUser);
                    NotificationHelper.ShowSuccess($"User '{Username}' created successfully.");
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed(_isEditMode ? "update user" : "create user", ex.Message);
            }
        }
    }
}
