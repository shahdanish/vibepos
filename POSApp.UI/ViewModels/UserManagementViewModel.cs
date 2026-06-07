using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class UserManagementViewModel : ViewModelBase
    {
        private readonly IUserRepository _userRepository;
        private string _searchText = string.Empty;
        private bool _showInactive;

        public ObservableCollection<User> Users { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = LoadDataAsync(); }
        }

        public bool ShowInactive
        {
            get => _showInactive;
            set { if (SetProperty(ref _showInactive, value)) _ = LoadDataAsync(); }
        }

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }

        public event Action<User?>? OpenFormRequested;

        public UserManagementViewModel(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            AddCommand          = new RelayCommand(_ => OpenFormRequested?.Invoke(null));
            EditCommand         = new RelayCommand(p => OpenFormRequested?.Invoke(p as User), p => p is User);
            ToggleActiveCommand = new RelayCommand(async p => await ToggleActiveAsync(p as User), p => p is User);
            DeleteCommand       = new RelayCommand(async p => await DeleteAsync(p as User), p => p is User);
            RefreshCommand      = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            var all = await _userRepository.GetAllWithRolesAsync(includeInactive: ShowInactive);

            if (!string.IsNullOrWhiteSpace(SearchText))
                all = all.Where(u => u.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                                  || u.RoleName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            Users.Clear();
            foreach (var u in all) Users.Add(u);
        }

        private async Task ToggleActiveAsync(User? user)
        {
            if (user == null) return;

            // Prevent disabling yourself
            if (user.Id == SessionManager.CurrentUser?.Id)
            {
                NotificationHelper.ValidationErrorCustom("You cannot deactivate your own account.");
                return;
            }

            var action = user.IsActive ? "deactivate" : "activate";
            if (!NotificationHelper.Confirm($"{char.ToUpper(action[0]) + action[1..]} '{user.Username}'?", "Confirm"))
                return;

            try
            {
                user.IsActive = !user.IsActive;
                await _userRepository.UpdateAsync(user);
                NotificationHelper.ShowSuccess($"User '{user.Username}' has been {action}d.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                user.IsActive = !user.IsActive;
                NotificationHelper.OperationFailed(action + " user", ex.Message);
            }
        }

        private async Task DeleteAsync(User? user)
        {
            if (user == null) return;

            if (user.Id == SessionManager.CurrentUser?.Id)
            {
                NotificationHelper.ValidationErrorCustom("You cannot delete your own account.");
                return;
            }

            if (!NotificationHelper.Confirm($"Deactivate user '{user.Username}'?\n\nThe user will no longer be able to log in.", "Deactivate User"))
                return;

            try
            {
                await _userRepository.DeleteAsync(user.Id);
                NotificationHelper.ShowInfo($"User '{user.Username}' has been deactivated.", "User Deactivated");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("deactivate user", ex.Message);
            }
        }
    }
}
