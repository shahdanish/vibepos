using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class ShiftViewModel : ViewModelBase
    {
        private readonly IShiftRepository _shiftRepository;

        private Shift? _currentShift;
        private decimal _openingBalance;
        private decimal _closingBalance;
        private bool _isShiftOpen;
        private string _shiftStatus = "No shift open";

        public ObservableCollection<Shift> ShiftHistory { get; } = new();

        public Shift? CurrentShift
        {
            get => _currentShift;
            set => SetProperty(ref _currentShift, value);
        }

        public decimal OpeningBalance
        {
            get => _openingBalance;
            set => SetProperty(ref _openingBalance, value);
        }

        public decimal ClosingBalance
        {
            get => _closingBalance;
            set => SetProperty(ref _closingBalance, value);
        }

        public bool IsShiftOpen
        {
            get => _isShiftOpen;
            set => SetProperty(ref _isShiftOpen, value);
        }

        public string ShiftStatus
        {
            get => _shiftStatus;
            set => SetProperty(ref _shiftStatus, value);
        }

        public ICommand OpenShiftCommand { get; }
        public ICommand CloseShiftCommand { get; }
        public ICommand RefreshCommand { get; }

        public ShiftViewModel(IShiftRepository shiftRepository)
        {
            _shiftRepository = shiftRepository;

            OpenShiftCommand = new RelayCommand(async _ => await OpenShift(), _ => !IsShiftOpen);
            CloseShiftCommand = new RelayCommand(async _ => await CloseShift(), _ => IsShiftOpen);
            RefreshCommand = new RelayCommand(async _ => await LoadData());

            _ = LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                CurrentShift = await _shiftRepository.GetCurrentOpenShiftAsync();
                IsShiftOpen = CurrentShift != null;

                if (IsShiftOpen)
                {
                    ShiftStatus = $"Shift open since {CurrentShift!.OpenedAt:hh:mm tt} | Opening: Rs.{CurrentShift.OpeningBalance:N2}";
                }
                else
                {
                    ShiftStatus = "No shift open — please open a shift to begin.";
                }

                var history = await _shiftRepository.GetAllAsync();
                ShiftHistory.Clear();
                foreach (var shift in history)
                {
                    ShiftHistory.Add(shift);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading shift data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OpenShift()
        {
            if (OpeningBalance < 0)
            {
                MessageBox.Show("Opening balance cannot be negative.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _shiftRepository.OpenShiftAsync(OpeningBalance);
                OpeningBalance = 0;
                await LoadData();

                MessageBox.Show("Shift opened successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening shift: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CloseShift()
        {
            if (CurrentShift == null) return;

            if (ClosingBalance < 0)
            {
                MessageBox.Show("Closing balance cannot be negative.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _shiftRepository.CloseShiftAsync(CurrentShift.Id, ClosingBalance);
                
                // Reload to get the calculated expected balance
                var closedShifts = await _shiftRepository.GetAllAsync();
                var justClosed = closedShifts.FirstOrDefault();

                if (justClosed != null)
                {
                    var diff = justClosed.Difference;
                    var diffText = diff >= 0 ? $"+Rs.{diff:N2}" : $"-Rs.{Math.Abs(diff):N2}";
                    
                    MessageBox.Show(
                        $"Shift Closed!\n\n" +
                        $"Expected: Rs.{justClosed.ExpectedClosingBalance:N2}\n" +
                        $"Actual: Rs.{justClosed.ActualClosingBalance:N2}\n" +
                        $"Difference: {diffText}",
                        "Shift Summary", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ClosingBalance = 0;
                await LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing shift: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
