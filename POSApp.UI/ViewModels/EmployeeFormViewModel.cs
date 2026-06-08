using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class EmployeeFormViewModel : ViewModelBase
    {
        private readonly IEmployeeRepository _employeeRepository;
        private int _employeeId;

        private string _employeeCode = string.Empty;
        private string _name = string.Empty;
        private string _fatherName = string.Empty;
        private string _cnic = string.Empty;
        private string _cellNumber = string.Empty;
        private string _designation = string.Empty;
        private string _department = string.Empty;
        private DateTime _joiningDate = DateTime.Today;
        private decimal _basicSalary;
        private string _address = string.Empty;
        private string _email = string.Empty;
        private bool _isActive = true;

        public string FormTitle => _employeeId == 0 ? "Add Employee" : "Edit Employee";

        public string EmployeeCode
        {
            get => _employeeCode;
            set => SetProperty(ref _employeeCode, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string FatherName
        {
            get => _fatherName;
            set => SetProperty(ref _fatherName, value);
        }

        public string Cnic
        {
            get => _cnic;
            set => SetProperty(ref _cnic, value);
        }

        public string CellNumber
        {
            get => _cellNumber;
            set => SetProperty(ref _cellNumber, value);
        }

        public string Designation
        {
            get => _designation;
            set => SetProperty(ref _designation, value);
        }

        public string Department
        {
            get => _department;
            set => SetProperty(ref _department, value);
        }

        public DateTime JoiningDate
        {
            get => _joiningDate;
            set => SetProperty(ref _joiningDate, value);
        }

        public decimal BasicSalary
        {
            get => _basicSalary;
            set => SetProperty(ref _basicSalary, value);
        }

        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ICommand SaveCommand { get; }
        public event Action<bool>? RequestClose;

        public EmployeeFormViewModel(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
            SaveCommand = new AsyncRelayCommand(SaveAsync);
        }

        public async Task LoadEmployeeAsync(Employee? employee)
        {
            if (employee == null)
            {
                EmployeeCode = await _employeeRepository.GenerateEmployeeCodeAsync();
                return;
            }

            _employeeId = employee.Id;
            EmployeeCode = employee.EmployeeCode;
            Name = employee.Name;
            FatherName = employee.FatherName ?? string.Empty;
            Cnic = employee.Cnic ?? string.Empty;
            CellNumber = employee.CellNumber ?? string.Empty;
            Designation = employee.Designation;
            Department = employee.Department ?? string.Empty;
            JoiningDate = employee.JoiningDate;
            BasicSalary = employee.BasicSalary;
            Address = employee.Address ?? string.Empty;
            Email = employee.Email ?? string.Empty;
            IsActive = employee.IsActive;
            OnPropertyChanged(nameof(FormTitle));
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            { NotificationHelper.ValidationError("Employee name is required."); return; }

            if (string.IsNullOrWhiteSpace(Designation))
            { NotificationHelper.ValidationError("Designation is required."); return; }

            if (!string.IsNullOrWhiteSpace(Cnic) &&
                await _employeeRepository.CnicExistsAsync(Cnic, _employeeId == 0 ? null : _employeeId))
            { NotificationHelper.ValidationError("An employee with this CNIC already exists."); return; }

            try
            {
                if (_employeeId == 0)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = EmployeeCode,
                        Name = Name.Trim(),
                        FatherName = string.IsNullOrWhiteSpace(FatherName) ? null : FatherName.Trim(),
                        Cnic = string.IsNullOrWhiteSpace(Cnic) ? null : Cnic.Trim(),
                        CellNumber = string.IsNullOrWhiteSpace(CellNumber) ? null : CellNumber.Trim(),
                        Designation = Designation.Trim(),
                        Department = string.IsNullOrWhiteSpace(Department) ? null : Department.Trim(),
                        JoiningDate = JoiningDate,
                        BasicSalary = BasicSalary,
                        Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                        IsActive = IsActive
                    };
                    await _employeeRepository.AddAsync(emp);
                    NotificationHelper.ShowSuccess($"Employee '{emp.Name}' added successfully.");
                }
                else
                {
                    var emp = await _employeeRepository.GetByIdAsync(_employeeId);
                    if (emp == null) return;

                    emp.Name = Name.Trim();
                    emp.FatherName = string.IsNullOrWhiteSpace(FatherName) ? null : FatherName.Trim();
                    emp.Cnic = string.IsNullOrWhiteSpace(Cnic) ? null : Cnic.Trim();
                    emp.CellNumber = string.IsNullOrWhiteSpace(CellNumber) ? null : CellNumber.Trim();
                    emp.Designation = Designation.Trim();
                    emp.Department = string.IsNullOrWhiteSpace(Department) ? null : Department.Trim();
                    emp.JoiningDate = JoiningDate;
                    emp.BasicSalary = BasicSalary;
                    emp.Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim();
                    emp.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                    emp.IsActive = IsActive;

                    await _employeeRepository.UpdateAsync(emp);
                    NotificationHelper.ShowSuccess($"Employee '{emp.Name}' updated.");
                }

                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save employee", ex.Message);
            }
        }
    }
}
