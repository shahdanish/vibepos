using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class PharmacyFormViewModel : ViewModelBase
    {
        private readonly IPharmacyRepository _pharmacyRepository;
        private Pharmacy? _editing;

        private string _name = string.Empty;
        private string? _ownerName;
        private string? _phone;
        private string? _city;
        private string? _area;
        private string? _address;
        private string? _licenseNo;
        private string? _ntn;
        private bool _isActive = true;

        public string FormTitle => _editing == null ? "Add Pharmacy" : "Edit Pharmacy";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? OwnerName
        {
            get => _ownerName;
            set => SetProperty(ref _ownerName, value);
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

        public string? Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? LicenseNo
        {
            get => _licenseNo;
            set => SetProperty(ref _licenseNo, value);
        }

        public string? Ntn
        {
            get => _ntn;
            set => SetProperty(ref _ntn, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        // Raised when the dialog should close; bool = success
        public event Action<bool>? CloseRequested;

        public PharmacyFormViewModel(IPharmacyRepository pharmacyRepository)
        {
            _pharmacyRepository = pharmacyRepository;
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false));
        }

        public void LoadPharmacy(Pharmacy? pharmacy)
        {
            _editing = pharmacy;

            if (pharmacy != null)
            {
                Name = pharmacy.Name;
                OwnerName = pharmacy.OwnerName;
                Phone = pharmacy.Phone;
                City = pharmacy.City;
                Area = pharmacy.Area;
                Address = pharmacy.Address;
                LicenseNo = pharmacy.LicenseNo;
                Ntn = pharmacy.Ntn;
                IsActive = pharmacy.IsActive;
            }
            else
            {
                Name = string.Empty;
                OwnerName = null;
                Phone = null;
                City = null;
                Area = null;
                Address = null;
                LicenseNo = null;
                Ntn = null;
                IsActive = true;
            }

            OnPropertyChanged(nameof(FormTitle));
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NotificationHelper.ValidationError("Pharmacy Name");
                return;
            }

            if (!string.IsNullOrWhiteSpace(LicenseNo))
            {
                var isUnique = await _pharmacyRepository.IsLicenseNoUniqueAsync(LicenseNo.Trim(), _editing?.Id);
                if (!isUnique)
                {
                    NotificationHelper.ValidationErrorCustom($"License No '{LicenseNo}' is already in use by another pharmacy.");
                    return;
                }
            }

            try
            {
                if (_editing == null)
                {
                    var pharmacy = new Pharmacy
                    {
                        Name = Name.Trim(),
                        OwnerName = NullIfEmpty(OwnerName),
                        Phone = NullIfEmpty(Phone),
                        City = NullIfEmpty(City),
                        Area = NullIfEmpty(Area),
                        Address = NullIfEmpty(Address),
                        LicenseNo = NullIfEmpty(LicenseNo),
                        Ntn = NullIfEmpty(Ntn),
                        IsActive = IsActive,
                    };
                    await _pharmacyRepository.AddAsync(pharmacy);
                    NotificationHelper.ShowSuccess($"Pharmacy '{pharmacy.Name}' added successfully.", "Pharmacy Added");
                }
                else
                {
                    _editing.Name = Name.Trim();
                    _editing.OwnerName = NullIfEmpty(OwnerName);
                    _editing.Phone = NullIfEmpty(Phone);
                    _editing.City = NullIfEmpty(City);
                    _editing.Area = NullIfEmpty(Area);
                    _editing.Address = NullIfEmpty(Address);
                    _editing.LicenseNo = NullIfEmpty(LicenseNo);
                    _editing.Ntn = NullIfEmpty(Ntn);
                    _editing.IsActive = IsActive;
                    await _pharmacyRepository.UpdateAsync(_editing);
                    NotificationHelper.ShowSuccess($"Pharmacy '{_editing.Name}' updated successfully.", "Pharmacy Updated");
                }

                CloseRequested?.Invoke(true);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed(_editing == null ? "add pharmacy" : "update pharmacy", ex.Message);
            }
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
