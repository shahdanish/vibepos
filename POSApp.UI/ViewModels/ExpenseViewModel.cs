using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class ExpenseViewModel : ViewModelBase
    {
        private readonly IExpenseRepository _expenseRepository;

        private string _description = string.Empty;
        private decimal _amount;
        private string? _note;
        private DateTime _filterDate = DateTime.Now.Date;
        private decimal _totalExpenses;

        public ObservableCollection<Expense> Expenses { get; } = new();

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public decimal Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        public string? Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        public DateTime FilterDate
        {
            get => _filterDate;
            set
            {
                if (SetProperty(ref _filterDate, value))
                    _ = LoadExpenses();
            }
        }

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        public ICommand AddExpenseCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand RefreshCommand { get; }

        public ExpenseViewModel(IExpenseRepository expenseRepository)
        {
            _expenseRepository = expenseRepository;

            AddExpenseCommand = new RelayCommand(async _ => await AddExpense());
            DeleteExpenseCommand = new RelayCommand(async param => await DeleteExpense(param));
            RefreshCommand = new RelayCommand(async _ => await LoadExpenses());

            _ = LoadExpenses();
        }

        private async Task LoadExpenses()
        {
            try
            {
                var expenses = await _expenseRepository.GetByDateRangeAsync(FilterDate, FilterDate);
                Expenses.Clear();
                foreach (var expense in expenses)
                {
                    Expenses.Add(expense);
                }
                TotalExpenses = Expenses.Sum(e => e.Amount);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expenses: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddExpense()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                MessageBox.Show("Please enter a description.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var expense = new Expense
                {
                    Description = Description,
                    Amount = Amount,
                    Date = DateTime.Now,
                    Note = Note
                };

                await _expenseRepository.AddAsync(expense);

                // Reset form
                Description = string.Empty;
                Amount = 0;
                Note = null;

                await LoadExpenses();

                MessageBox.Show("Expense added successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding expense: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteExpense(object? param)
        {
            if (param is Expense expense)
            {
                var result = MessageBox.Show($"Delete expense '{expense.Description}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await _expenseRepository.DeleteAsync(expense.Id);
                    await LoadExpenses();
                }
            }
        }
    }
}
