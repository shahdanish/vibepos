using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class DoctorFormViewModel : ViewModelBase
    {
        private readonly IDoctorRepository _doctorRepository;
        private Doctor? _editing;

        private string _name = string.Empty;
        private string? _specialization;
        private string? _phone;
        private string? _city;
        private string? _address;
        private string? _pmdcLicenseNo;
        private bool _isActive = true;

        public string FormTitle => _editing == null ? "Add Doctor" : "Edit Doctor";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? Specialization
        {
            get => _specialization;
            set => SetProperty(ref _specialization, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? City
        {
            get => _city;
            set => SetProperty(ref _city, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? PmdcLicenseNo
        {
            get => _pmdcLicenseNo;
            set => SetProperty(ref _pmdcLicenseNo, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool>? CloseRequested;

        public DoctorFormViewModel(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public void LoadDoctor(Doctor? doctor)
        {
            _editing = doctor;

            if (doctor != null)
            {
                Name = doctor.Name;
                Specialization = doctor.Specialization;
                Phone = doctor.Phone;
                City = doctor.City;
                Address = doctor.Address;
                PmdcLicenseNo = doctor.PmdcLicenseNo;
                IsActive = doctor.IsActive;
            }
            else
            {
                Name = string.Empty;
                Specialization = null;
                Phone = null;
                City = null;
                Address = null;
                PmdcLicenseNo = null;
                IsActive = true;
            }

            OnPropertyChanged(nameof(FormTitle));
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NotificationHelper.ValidationError("Doctor Name");
                return;
            }

            // Warn on duplicate name but allow saving
            var nameExists = await _doctorRepository.NameExistsAsync(Name.Trim(), _editing?.Id);
            if (nameExists)
            {
                if (!NotificationHelper.Confirm(
                        $"A doctor named '{Name.Trim()}' already exists. Do you still want to save?",
                        "Duplicate Name Warning"))
                    return;
            }

            try
            {
                if (_editing == null)
                {
                    var doctor = new Doctor
                    {
                        Name = Name.Trim(),
                        Specialization = NullIfEmpty(Specialization),
                        Phone = NullIfEmpty(Phone),
                        City = NullIfEmpty(City),
                        Address = NullIfEmpty(Address),
                        PmdcLicenseNo = NullIfEmpty(PmdcLicenseNo),
                        IsActive = IsActive,
                    };
                    await _doctorRepository.AddAsync(doctor);
                    NotificationHelper.ShowSuccess($"Dr. '{doctor.Name}' added successfully.", "Doctor Added");
                }
                else
                {
                    _editing.Name = Name.Trim();
                    _editing.Specialization = NullIfEmpty(Specialization);
                    _editing.Phone = NullIfEmpty(Phone);
                    _editing.City = NullIfEmpty(City);
                    _editing.Address = NullIfEmpty(Address);
                    _editing.PmdcLicenseNo = NullIfEmpty(PmdcLicenseNo);
                    _editing.IsActive = IsActive;
                    await _doctorRepository.UpdateAsync(_editing);
                    NotificationHelper.ShowSuccess($"Dr. '{_editing.Name}' updated successfully.", "Doctor Updated");
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed(_editing == null ? "add doctor" : "update doctor", ex.Message);
            }
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
