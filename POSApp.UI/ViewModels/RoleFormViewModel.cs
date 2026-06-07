using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    // Wrapper for a single permission with a selection state
    public sealed class PermissionItem : ViewModelBase
    {
        private bool _isSelected;
        public Permission Permission { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public PermissionItem(Permission permission, bool isSelected = false)
        {
            Permission = permission;
            _isSelected = isSelected;
        }
    }

    // Group of permissions under a category header
    public sealed class PermissionCategory
    {
        public string Category { get; }
        public ObservableCollection<PermissionItem> Items { get; } = new();

        public PermissionCategory(string category) => Category = category;

        public void SelectAll()
        {
            foreach (var item in Items) item.IsSelected = true;
        }

        public void DeselectAll()
        {
            foreach (var item in Items) item.IsSelected = false;
        }
    }

    public sealed class RoleFormViewModel : ViewModelBase
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IPermissionRepository _permissionRepository;

        private Role? _editingRole;
        private string _formTitle = "Add Role";
        private string _roleName = string.Empty;
        private string _roleDescription = string.Empty;
        private bool _isEditMode;

        public string FormTitle
        {
            get => _formTitle;
            set => SetProperty(ref _formTitle, value);
        }

        public string RoleName
        {
            get => _roleName;
            set => SetProperty(ref _roleName, value);
        }

        public string RoleDescription
        {
            get => _roleDescription;
            set => SetProperty(ref _roleDescription, value);
        }

        public bool IsEditMode => _isEditMode;

        public ObservableCollection<PermissionCategory> PermissionCategories { get; } = new();

        public ICommand SaveCommand { get; }
        public ICommand SelectAllCategoryCommand { get; }
        public ICommand DeselectAllCategoryCommand { get; }

        public event Action<bool>? CloseRequested;

        public RoleFormViewModel(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
        {
            _roleRepository = roleRepository;
            _permissionRepository = permissionRepository;

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            SelectAllCategoryCommand = new RelayCommand(p => (p as PermissionCategory)?.SelectAll());
            DeselectAllCategoryCommand = new RelayCommand(p => (p as PermissionCategory)?.DeselectAll());
        }

        public async Task LoadRoleAsync(Role? role)
        {
            var allPermissions = await _permissionRepository.GetAllAsync();

            HashSet<int> selectedIds = new();
            if (role != null)
            {
                var full = await _roleRepository.GetWithPermissionsAsync(role.Id);
                selectedIds = full?.RolePermissions.Select(rp => rp.PermissionId).ToHashSet() ?? new();
                _editingRole = full ?? role;
                _isEditMode = true;
                FormTitle = $"Edit Role — {role.Name}";
                RoleName = role.Name;
                RoleDescription = role.Description;
            }
            else
            {
                _editingRole = null;
                _isEditMode = false;
                FormTitle = "Add Role";
                RoleName = string.Empty;
                RoleDescription = string.Empty;
            }

            // Build grouped permission categories
            PermissionCategories.Clear();
            foreach (var group in allPermissions.GroupBy(p => p.Category).OrderBy(g => g.Key))
            {
                var cat = new PermissionCategory(group.Key);
                foreach (var p in group)
                    cat.Items.Add(new PermissionItem(p, selectedIds.Contains(p.Id)));
                PermissionCategories.Add(cat);
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(RoleName))
            {
                NotificationHelper.ValidationErrorCustom("Role name is required.");
                return;
            }

            var selectedPermIds = PermissionCategories
                .SelectMany(c => c.Items)
                .Where(item => item.IsSelected)
                .Select(item => item.Permission.Id)
                .ToList();

            try
            {
                if (_isEditMode && _editingRole != null)
                {
                    _editingRole.Name = RoleName.Trim();
                    _editingRole.Description = RoleDescription.Trim();
                    await _roleRepository.UpdateAsync(_editingRole);
                    await _roleRepository.SetPermissionsAsync(_editingRole.Id, selectedPermIds);
                    NotificationHelper.ShowSuccess($"Role '{RoleName}' updated.");
                }
                else
                {
                    var newRole = new Role
                    {
                        Name = RoleName.Trim(),
                        Description = RoleDescription.Trim(),
                        IsSystemRole = false,
                        CreatedDate = DateTime.Now
                    };
                    var created = await _roleRepository.CreateAsync(newRole);
                    await _roleRepository.SetPermissionsAsync(created.Id, selectedPermIds);
                    NotificationHelper.ShowSuccess($"Role '{RoleName}' created.");
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed(_isEditMode ? "update role" : "create role", ex.Message);
            }
        }
    }
}
