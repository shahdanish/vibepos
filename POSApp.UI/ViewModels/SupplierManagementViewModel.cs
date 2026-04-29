using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class SupplierManagementViewModel : ViewModelBase
    {
        private readonly ISupplierRepository _supplierRepository;

        private string _searchText = string.Empty;
        private Supplier? _selectedSupplier;
        private string _supplierId = string.Empty;
        private string _name = string.Empty;
        private string? _contactPerson;
        private string? _phone;
        private string? _email;
        private string? _address;
        private string? _paymentTerms;
        private bool _isEditMode;

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = SearchSuppliers();
                }
            }
        }

        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value) && value != null)
                {
                    EditSupplier(value);
                }
            }
        }

        public string SupplierId
        {
            get => _supplierId;
            set => SetProperty(ref _supplierId, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? ContactPerson
        {
            get => _contactPerson;
            set => SetProperty(ref _contactPerson, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? PaymentTerms
        {
            get => _paymentTerms;
            set => SetProperty(ref _paymentTerms, value);
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand NewCommand { get; }

        public SupplierManagementViewModel(ISupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;

            SaveCommand = new RelayCommand(async _ => await SaveSupplier());
            CancelCommand = new RelayCommand(_ => CancelEdit());
            DeleteCommand = new RelayCommand(async _ => await DeleteSupplier());
            NewCommand = new RelayCommand(_ => NewSupplier());

            _ = LoadSuppliers();
        }

        private async Task LoadSuppliers()
        {
            var suppliers = await _supplierRepository.GetAllAsync();
            Suppliers.Clear();
            foreach (var supplier in suppliers)
            {
                Suppliers.Add(supplier);
            }
        }

        private async Task SearchSuppliers()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadSuppliers();
                return;
            }

            var suppliers = await _supplierRepository.SearchAsync(SearchText);
            Suppliers.Clear();
            foreach (var supplier in suppliers)
            {
                Suppliers.Add(supplier);
            }
        }

        private void EditSupplier(Supplier supplier)
        {
            SupplierId = supplier.SupplierId;
            Name = supplier.Name;
            ContactPerson = supplier.ContactPerson;
            Phone = supplier.Phone;
            Email = supplier.Email;
            Address = supplier.Address;
            PaymentTerms = supplier.PaymentTerms;
            IsEditMode = true;
        }

        private async Task SaveSupplier()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NotificationHelper.ValidationErrorCustom("Supplier name is required.");
                return;
            }

            try
            {
                if (IsEditMode)
                {
                    var existing = await _supplierRepository.GetBySupplierIdAsync(SupplierId);
                    if (existing != null)
                    {
                        existing.Name = Name;
                        existing.ContactPerson = ContactPerson;
                        existing.Phone = Phone;
                        existing.Email = Email;
                        existing.Address = Address;
                        existing.PaymentTerms = PaymentTerms;

                        await _supplierRepository.UpdateAsync(existing);
                        NotificationHelper.ShowSuccess("Supplier updated successfully!");
                    }
                }
                else
                {
                    var supplier = new Supplier
                    {
                        SupplierId = GenerateSupplierId(),
                        Name = Name,
                        ContactPerson = ContactPerson,
                        Phone = Phone,
                        Email = Email,
                        Address = Address,
                        PaymentTerms = PaymentTerms
                    };

                    await _supplierRepository.AddAsync(supplier);
                    NotificationHelper.ShowSuccess("Supplier added successfully!");
                }

                ClearForm();
                await LoadSuppliers();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save supplier", ex.Message);
            }
        }

        private async Task DeleteSupplier()
        {
            if (SelectedSupplier == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a supplier to delete.");
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{SelectedSupplier.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    await _supplierRepository.DeleteAsync(SelectedSupplier.Id);
                    NotificationHelper.ShowSuccess("Supplier deleted successfully!");
                    ClearForm();
                    await LoadSuppliers();
                }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("delete supplier", ex.Message);
                }
            }
        }

        private void NewSupplier()
        {
            ClearForm();
        }

        private void CancelEdit()
        {
            ClearForm();
        }

        private void ClearForm()
        {
            SupplierId = string.Empty;
            Name = string.Empty;
            ContactPerson = null;
            Phone = null;
            Email = null;
            Address = null;
            PaymentTerms = null;
            IsEditMode = false;
            SelectedSupplier = null;
        }

        private string GenerateSupplierId()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(100, 999);
            return $"SUP-{timestamp}-{random}";
        }
    }
}
