using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class DailySummaryViewModel : ViewModelBase
    {
        private readonly IDailySalesSummaryRepository _dailySummaryRepository;
        private readonly ISaleRepository _saleRepository;
        private readonly IExpenseRepository _expenseRepository;
        
        private DateTime _selectedDate = DateTime.Today;
        private decimal _openingBalance;
        private decimal _totalSales;
        private decimal _totalExpenses;
        private decimal _expectedClosing;
        private decimal _actualClosing;
        private decimal _variance;
        private string? _notes;

        public ObservableCollection<DailySalesSummary> Summaries { get; } = new();

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    _ = LoadSummaryForDate();
                }
            }
        }

        public decimal OpeningBalance
        {
            get => _openingBalance;
            set
            {
                if (SetProperty(ref _openingBalance, value))
                {
                    CalculateExpectedClosing();
                }
            }
        }

        public decimal TotalSales
        {
            get => _totalSales;
            set => SetProperty(ref _totalSales, value);
        }

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set
            {
                if (SetProperty(ref _totalExpenses, value))
                {
                    CalculateExpectedClosing();
                }
            }
        }

        public decimal ExpectedClosing
        {
            get => _expectedClosing;
            set => SetProperty(ref _expectedClosing, value);
        }

        public decimal ActualClosing
        {
            get => _actualClosing;
            set
            {
                if (SetProperty(ref _actualClosing, value))
                {
                    CalculateVariance();
                }
            }
        }

        public decimal Variance
        {
            get => _variance;
            set => SetProperty(ref _variance, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand RefreshCommand { get; }

        public DailySummaryViewModel(
            IDailySalesSummaryRepository dailySummaryRepository,
            ISaleRepository saleRepository,
            IExpenseRepository expenseRepository)
        {
            _dailySummaryRepository = dailySummaryRepository;
            _saleRepository = saleRepository;
            _expenseRepository = expenseRepository;

            SaveCommand = new RelayCommand(async _ => await SaveSummary());
            RefreshCommand = new RelayCommand(async _ => await LoadSummaryForDate());

            _ = LoadSummaries();
            _ = LoadSummaryForDate();
        }

        private async Task LoadSummaries()
        {
            var summaries = await _dailySummaryRepository.GetAllAsync();
            Summaries.Clear();
            foreach (var summary in summaries)
            {
                Summaries.Add(summary);
            }
        }

        private async Task LoadSummaryForDate()
        {
            var existing = await _dailySummaryRepository.GetByDateAsync(SelectedDate);
            
            if (existing != null)
            {
                OpeningBalance = existing.OpeningBalance;
                TotalSales = existing.TotalSales;
                TotalExpenses = existing.TotalExpenses;
                ExpectedClosing = existing.ExpectedClosing;
                ActualClosing = existing.ActualClosing;
                Variance = existing.Variance;
                Notes = existing.Notes;
            }
            else
            {
                var sales = await _saleRepository.GetByDateAsync(SelectedDate);
                TotalSales = sales.Sum(s => s.TotalBill);

                var expenses = await _expenseRepository.GetByDateRangeAsync(SelectedDate, SelectedDate);
                TotalExpenses = expenses.Sum(e => e.Amount);

                OpeningBalance = 0;
                ActualClosing = 0;
                Notes = string.Empty;
                
                CalculateExpectedClosing();
            }
        }

        private void CalculateExpectedClosing()
        {
            ExpectedClosing = OpeningBalance + TotalSales - TotalExpenses;
            CalculateVariance();
        }

        private void CalculateVariance()
        {
            Variance = ActualClosing - ExpectedClosing;
        }

        private async Task SaveSummary()
        {
            try
            {
                var existing = await _dailySummaryRepository.GetByDateAsync(SelectedDate);
                
                var summary = existing ?? new DailySalesSummary
                {
                    Date = SelectedDate
                };

                summary.OpeningBalance = OpeningBalance;
                summary.TotalSales = TotalSales;
                summary.TotalExpenses = TotalExpenses;
                summary.ExpectedClosing = ExpectedClosing;
                summary.ActualClosing = ActualClosing;
                summary.Variance = Variance;
                summary.Notes = Notes;

                if (existing != null)
                {
                    await _dailySummaryRepository.UpdateAsync(summary);
                }
                else
                {
                    await _dailySummaryRepository.AddAsync(summary);
                }

                NotificationHelper.ShowSuccess("Daily summary saved successfully!");
                await LoadSummaries();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save daily summary", ex.Message);
            }
        }
    }
}
