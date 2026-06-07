using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class PharmacyManagementViewModel : ViewModelBase
    {
        private readonly IPharmacyRepository _pharmacyRepository;

        private string _searchText = string.Empty;
        private bool _showInactive = false;

        public ObservableCollection<Pharmacy> Pharmacies { get; } = new();

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
        public ICommand RefreshCommand { get; }

        // Raised when the view should open the add/edit form dialog
        public event Action<Pharmacy?>? OpenFormRequested;

        public PharmacyManagementViewModel(IPharmacyRepository pharmacyRepository)
        {
            _pharmacyRepository = pharmacyRepository;

            AddCommand = new RelayCommand(_ => OpenFormRequested?.Invoke(null));
            EditCommand = new RelayCommand(p => OpenFormRequested?.Invoke(p as Pharmacy), p => p is Pharmacy);
            ToggleActiveCommand = new RelayCommand(async p => await ToggleActiveAsync(p as Pharmacy), p => p is Pharmacy);
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as Pharmacy), p => p is Pharmacy);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IEnumerable<Pharmacy> results;

            if (string.IsNullOrWhiteSpace(SearchText))
                results = await _pharmacyRepository.GetAllAsync(ShowInactive);
            else
                results = await _pharmacyRepository.SearchAsync(SearchText, ShowInactive);

            Pharmacies.Clear();
            foreach (var p in results)
                Pharmacies.Add(p);
        }

        private async Task ToggleActiveAsync(Pharmacy? pharmacy)
        {
            if (pharmacy == null) return;

            var action = pharmacy.IsActive ? "deactivate" : "activate";
            var msg = pharmacy.IsActive
                ? $"Deactivate '{pharmacy.Name}'? It will be hidden from the sale screen dropdown."
                : $"Activate '{pharmacy.Name}'?";

            if (!NotificationHelper.Confirm(msg, $"Confirm {char.ToUpper(action[0]) + action[1..]}"))
                return;

            try
            {
                pharmacy.IsActive = !pharmacy.IsActive;
                await _pharmacyRepository.UpdateAsync(pharmacy);
                NotificationHelper.ShowSuccess($"Pharmacy '{pharmacy.Name}' has been {action}d.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                pharmacy.IsActive = !pharmacy.IsActive;
                NotificationHelper.OperationFailed(action + " pharmacy", ex.Message);
            }
        }

        private async Task DeleteAsync(Pharmacy? pharmacy)
        {
            if (pharmacy == null) return;

            if (!NotificationHelper.Confirm(
                    $"Are you sure you want to delete '{pharmacy.Name}'?\n\nHistorical sale records will not be affected.",
                    "Delete Pharmacy"))
                return;

            try
            {
                await _pharmacyRepository.SoftDeleteAsync(pharmacy.Id);
                NotificationHelper.ShowInfo($"Pharmacy '{pharmacy.Name}' has been deleted.", "Pharmacy Deleted");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete pharmacy", ex.Message);
            }
        }
    }
}
