using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class EmployeeManagementViewModel : ViewModelBase
    {
        private readonly IEmployeeRepository _employeeRepository;

        private string _searchText = string.Empty;
        private bool _showInactive = false;

        public ObservableCollection<Employee> Employees { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    _ = LoadDataAsync();
            }
        }

        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (SetProperty(ref _showInactive, value))
                    _ = LoadDataAsync();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand GenerateSalarySlipCommand { get; }
        public ICommand RefreshCommand { get; }

        public event Action<Employee?>? OpenFormRequested;
        public event Action<Employee>? GenerateSalarySlipRequested;

        public EmployeeManagementViewModel(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;

            AddCommand = new RelayCommand(_ => OpenFormRequested?.Invoke(null));
            EditCommand = new RelayCommand(p => OpenFormRequested?.Invoke(p as Employee), p => p is Employee);
            ToggleActiveCommand = new RelayCommand(async p => await ToggleActiveAsync(p as Employee), p => p is Employee);
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as Employee), p => p is Employee);
            GenerateSalarySlipCommand = new RelayCommand(
                p => GenerateSalarySlipRequested?.Invoke((Employee)p!),
                p => p is Employee);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IEnumerable<Employee> results = string.IsNullOrWhiteSpace(SearchText)
                ? await _employeeRepository.GetAllAsync(ShowInactive)
                : await _employeeRepository.SearchAsync(SearchText, ShowInactive);

            Employees.Clear();
            foreach (var e in results)
                Employees.Add(e);
        }

        private async Task ToggleActiveAsync(Employee? emp)
        {
            if (emp == null) return;
            var action = emp.IsActive ? "deactivate" : "activate";
            if (!NotificationHelper.Confirm(
                    emp.IsActive
                        ? $"Deactivate '{emp.Name}'? They will be hidden from active lists."
                        : $"Activate '{emp.Name}'?",
                    $"Confirm {char.ToUpper(action[0]) + action[1..]}"))
                return;

            try
            {
                emp.IsActive = !emp.IsActive;
                await _employeeRepository.UpdateAsync(emp);
                NotificationHelper.ShowSuccess($"'{emp.Name}' has been {action}d.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                emp.IsActive = !emp.IsActive;
                NotificationHelper.OperationFailed(action + " employee", ex.Message);
            }
        }

        private async Task DeleteAsync(Employee? emp)
        {
            if (emp == null) return;
            if (!NotificationHelper.Confirm(
                    $"Delete employee '{emp.Name}'? This cannot be undone.",
                    "Delete Employee"))
                return;

            try
            {
                await _employeeRepository.SoftDeleteAsync(emp.Id);
                NotificationHelper.ShowInfo($"'{emp.Name}' has been deleted.", "Employee Deleted");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete employee", ex.Message);
            }
        }
    }
}
