using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class RoleManagementViewModel : ViewModelBase
    {
        private readonly IRoleRepository _roleRepository;

        public ObservableCollection<Role> Roles { get; } = new();

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }

        public event Action<Role?>? OpenFormRequested;

        public RoleManagementViewModel(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;

            AddCommand     = new RelayCommand(_ => OpenFormRequested?.Invoke(null));
            EditCommand    = new RelayCommand(p => OpenFormRequested?.Invoke(p as Role), p => p is Role);
            DeleteCommand  = new RelayCommand(async p => await DeleteAsync(p as Role), p => p is Role r && !r.IsSystemRole);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            var roles = await _roleRepository.GetAllAsync();
            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);
        }

        public int GetPermissionCount(Role role) => role.RolePermissions.Count;

        private async Task DeleteAsync(Role? role)
        {
            if (role == null) return;

            if (role.IsSystemRole)
            {
                NotificationHelper.ValidationErrorCustom("System roles cannot be deleted.");
                return;
            }

            if (!NotificationHelper.Confirm(
                    $"Delete role '{role.Name}'?\n\nUsers assigned this role will need to be reassigned.",
                    "Delete Role"))
                return;

            try
            {
                await _roleRepository.DeleteAsync(role.Id);
                NotificationHelper.ShowInfo($"Role '{role.Name}' has been deleted.", "Role Deleted");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete role", ex.Message);
            }
        }
    }
}
