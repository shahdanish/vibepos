using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class DoctorManagementViewModel : ViewModelBase
    {
        private readonly IDoctorRepository _doctorRepository;

        private string _searchText = string.Empty;
        private bool _showInactive = false;

        public ObservableCollection<Doctor> Doctors { get; } = new();

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

        public event Action<Doctor?>? OpenFormRequested;

        public DoctorManagementViewModel(IDoctorRepository doctorRepository)
        {
            _doctorRepository = doctorRepository;

            AddCommand = new RelayCommand(_ => OpenFormRequested?.Invoke(null));
            EditCommand = new RelayCommand(p => OpenFormRequested?.Invoke(p as Doctor), p => p is Doctor);
            ToggleActiveCommand = new RelayCommand(async p => await ToggleActiveAsync(p as Doctor), p => p is Doctor);
            DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as Doctor), p => p is Doctor);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IEnumerable<Doctor> results = string.IsNullOrWhiteSpace(SearchText)
                ? await _doctorRepository.GetAllAsync(ShowInactive)
                : await _doctorRepository.SearchAsync(SearchText, ShowInactive);

            Doctors.Clear();
            foreach (var d in results)
                Doctors.Add(d);
        }

        private async Task ToggleActiveAsync(Doctor? doctor)
        {
            if (doctor == null) return;

            var action = doctor.IsActive ? "deactivate" : "activate";
            var msg = doctor.IsActive
                ? $"Deactivate Dr. '{doctor.Name}'? They will be hidden from the sale screen dropdown."
                : $"Activate Dr. '{doctor.Name}'?";

            if (!NotificationHelper.Confirm(msg, $"Confirm {char.ToUpper(action[0]) + action[1..]}"))
                return;

            try
            {
                doctor.IsActive = !doctor.IsActive;
                await _doctorRepository.UpdateAsync(doctor);
                NotificationHelper.ShowSuccess($"Dr. '{doctor.Name}' has been {action}d.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                doctor.IsActive = !doctor.IsActive;
                NotificationHelper.OperationFailed(action + " doctor", ex.Message);
            }
        }

        private async Task DeleteAsync(Doctor? doctor)
        {
            if (doctor == null) return;

            if (!NotificationHelper.Confirm(
                    $"Are you sure you want to delete Dr. {doctor.Name}? This action cannot be undone.",
                    "Delete Doctor"))
                return;

            try
            {
                await _doctorRepository.SoftDeleteAsync(doctor.Id);
                NotificationHelper.ShowInfo($"Dr. '{doctor.Name}' has been deleted.", "Doctor Deleted");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete doctor", ex.Message);
            }
        }
    }
}
