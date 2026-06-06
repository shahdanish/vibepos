using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class ExpenseViewModel : ViewModelBase
    {
        private readonly IExpenseRepository _expenseRepository;

        // Add-expense form
        private string _description = string.Empty;
        private decimal _amount;
        private string? _note;
        private decimal _totalExpenses;

        // Report period selection
        private string _selectedPeriod = "Daily";
        private DateTime _filterDate = DateTime.Now.Date;
        private DateTime _customStartDate = DateTime.Now.Date;
        private DateTime _customEndDate = DateTime.Now.Date;

        public ObservableCollection<Expense> Expenses { get; } = new();

        // ── Add-expense form properties ──────────────────────────────────────

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

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        // ── Period selector properties ────────────────────────────────────────

        public string SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetProperty(ref _selectedPeriod, value))
                {
                    OnPropertyChanged(nameof(IsDailyPeriod));
                    OnPropertyChanged(nameof(IsWeeklyPeriod));
                    OnPropertyChanged(nameof(IsMonthlyPeriod));
                    OnPropertyChanged(nameof(IsYearlyPeriod));
                    OnPropertyChanged(nameof(IsCustomPeriod));
                    OnPropertyChanged(nameof(PeriodDescription));
                    OnPropertyChanged(nameof(ShowDateNavigator));
                    _ = LoadExpenses();
                }
            }
        }

        public DateTime FilterDate
        {
            get => _filterDate;
            set
            {
                if (SetProperty(ref _filterDate, value))
                {
                    OnPropertyChanged(nameof(PeriodDescription));
                    if (!IsCustomPeriod) _ = LoadExpenses();
                }
            }
        }

        public DateTime CustomStartDate
        {
            get => _customStartDate;
            set
            {
                if (SetProperty(ref _customStartDate, value))
                {
                    OnPropertyChanged(nameof(PeriodDescription));
                    if (IsCustomPeriod) _ = LoadExpenses();
                }
            }
        }

        public DateTime CustomEndDate
        {
            get => _customEndDate;
            set
            {
                if (SetProperty(ref _customEndDate, value))
                {
                    OnPropertyChanged(nameof(PeriodDescription));
                    if (IsCustomPeriod) _ = LoadExpenses();
                }
            }
        }

        // Period-button IsChecked bindings (two-way via bool setters)
        public bool IsDailyPeriod   { get => _selectedPeriod == "Daily";   set { if (value) SelectedPeriod = "Daily"; } }
        public bool IsWeeklyPeriod  { get => _selectedPeriod == "Weekly";  set { if (value) SelectedPeriod = "Weekly"; } }
        public bool IsMonthlyPeriod { get => _selectedPeriod == "Monthly"; set { if (value) SelectedPeriod = "Monthly"; } }
        public bool IsYearlyPeriod  { get => _selectedPeriod == "Yearly";  set { if (value) SelectedPeriod = "Yearly"; } }
        public bool IsCustomPeriod  { get => _selectedPeriod == "Custom";  set { if (value) SelectedPeriod = "Custom"; } }

        // Hide nav arrows in Custom mode
        public bool ShowDateNavigator => !IsCustomPeriod;

        public string PeriodDescription => _selectedPeriod switch
        {
            "Daily"   => _filterDate.ToString("dddd, dd MMMM yyyy"),
            "Weekly"  => $"{WeekStart(_filterDate):dd MMM} – {WeekEnd(_filterDate):dd MMM yyyy}",
            "Monthly" => _filterDate.ToString("MMMM yyyy"),
            "Yearly"  => _filterDate.Year.ToString(),
            "Custom"  => $"{_customStartDate:dd MMM yyyy}  –  {_customEndDate:dd MMM yyyy}",
            _         => string.Empty
        };

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand AddExpenseCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PreviousPeriodCommand { get; }
        public ICommand NextPeriodCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand SetDailyCommand { get; }
        public ICommand SetWeeklyCommand { get; }
        public ICommand SetMonthlyCommand { get; }
        public ICommand SetYearlyCommand { get; }
        public ICommand SetCustomCommand { get; }

        public ExpenseViewModel(IExpenseRepository expenseRepository)
        {
            _expenseRepository = expenseRepository;

            AddExpenseCommand     = new RelayCommand(async _ => await AddExpense());
            DeleteExpenseCommand  = new RelayCommand(async p => await DeleteExpense(p));
            RefreshCommand        = new RelayCommand(async _ => await LoadExpenses());
            PreviousPeriodCommand = new RelayCommand(_ => NavigatePeriod(-1), _ => !IsCustomPeriod);
            NextPeriodCommand     = new RelayCommand(_ => NavigatePeriod(+1), _ => !IsCustomPeriod);
            PrintReportCommand    = new RelayCommand(_ => PrintExpenseReport());
            SetDailyCommand       = new RelayCommand(_ => SelectedPeriod = "Daily");
            SetWeeklyCommand      = new RelayCommand(_ => SelectedPeriod = "Weekly");
            SetMonthlyCommand     = new RelayCommand(_ => SelectedPeriod = "Monthly");
            SetYearlyCommand      = new RelayCommand(_ => SelectedPeriod = "Yearly");
            SetCustomCommand      = new RelayCommand(_ => SelectedPeriod = "Custom");

            _ = LoadExpenses();
        }

        // ── Date range helpers ────────────────────────────────────────────────

        private static DateTime WeekStart(DateTime d)
        {
            int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
            return d.AddDays(-diff).Date;
        }

        private static DateTime WeekEnd(DateTime d) => WeekStart(d).AddDays(6);

        private (DateTime start, DateTime end) GetDateRange() => _selectedPeriod switch
        {
            "Daily"   => (_filterDate.Date, _filterDate.Date),
            "Weekly"  => (WeekStart(_filterDate), WeekEnd(_filterDate)),
            "Monthly" => (new DateTime(_filterDate.Year, _filterDate.Month, 1),
                          new DateTime(_filterDate.Year, _filterDate.Month,
                                       DateTime.DaysInMonth(_filterDate.Year, _filterDate.Month))),
            "Yearly"  => (new DateTime(_filterDate.Year, 1, 1), new DateTime(_filterDate.Year, 12, 31)),
            "Custom"  => (_customStartDate.Date, _customEndDate.Date),
            _         => (_filterDate.Date, _filterDate.Date)
        };

        private void NavigatePeriod(int direction)
        {
            FilterDate = _selectedPeriod switch
            {
                "Daily"   => _filterDate.AddDays(direction),
                "Weekly"  => _filterDate.AddDays(7 * direction),
                "Monthly" => _filterDate.AddMonths(direction),
                "Yearly"  => _filterDate.AddYears(direction),
                _         => _filterDate
            };
        }

        // ── Data operations ───────────────────────────────────────────────────

        private async Task LoadExpenses()
        {
            try
            {
                var (start, end) = GetDateRange();
                var expenses = await _expenseRepository.GetByDateRangeAsync(start, end);
                Expenses.Clear();
                foreach (var e in expenses)
                    Expenses.Add(e);
                TotalExpenses = Expenses.Sum(e => e.Amount);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("load expenses", ex.Message);
            }
        }

        private async Task AddExpense()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                NotificationHelper.ValidationError("Description");
                return;
            }

            if (Amount <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Please enter a valid amount.");
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

                Description = string.Empty;
                Amount = 0;
                Note = null;

                await LoadExpenses();
                NotificationHelper.ShowSuccess("Expense added successfully!");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("add expense", ex.Message);
            }
        }

        private async Task DeleteExpense(object? param)
        {
            if (param is not Expense expense) return;

            if (NotificationHelper.Confirm($"Delete expense '{expense.Description}'?"))
            {
                await _expenseRepository.DeleteAsync(expense.Id);
                await LoadExpenses();
            }
        }

        // ── Print report ──────────────────────────────────────────────────────

        private void PrintExpenseReport()
        {
            if (!Expenses.Any())
            {
                NotificationHelper.ValidationErrorCustom("No expenses to print for the selected period.");
                return;
            }

            try
            {
                var doc = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left
                };

                // Store header
                var header = new Paragraph { Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center };
                header.Inlines.Add(new Bold(new Run("Shahjee super store")) { FontSize = 22 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 13 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("0332-3324911") { FontSize = 13 });
                doc.Blocks.Add(header);

                // Title
                doc.Blocks.Add(new Paragraph(new Bold(new Run("Expense Report")))
                {
                    TextAlignment = TextAlignment.Center,
                    FontSize = 15,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(3),
                    Margin = new Thickness(0, 2, 0, 1)
                });

                // Period description
                doc.Blocks.Add(new Paragraph(new Italic(new Run(PeriodDescription)))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                // Expenses table
                var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
                table.Columns.Add(new TableColumn { Width = new GridLength(0.35, GridUnitType.Star) }); // #
                table.Columns.Add(new TableColumn { Width = new GridLength(2.0, GridUnitType.Star) });  // Description
                table.Columns.Add(new TableColumn { Width = new GridLength(1.0, GridUnitType.Star) });  // Amount
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });  // Note

                var tg = new TableRowGroup();
                table.RowGroups.Add(tg);

                // Header row
                var headerRow = new TableRow { Background = Brushes.LightGray };
                headerRow.Cells.Add(HCell("#"));
                headerRow.Cells.Add(HCell("Description"));
                headerRow.Cells.Add(HCell("Amount (Rs.)"));
                headerRow.Cells.Add(HCell("Note"));
                tg.Rows.Add(headerRow);

                // Data rows
                int i = 1;
                foreach (var e in Expenses)
                {
                    var row = new TableRow();
                    row.Cells.Add(DCell(i.ToString(), TextAlignment.Center));
                    row.Cells.Add(DCell(e.Description, TextAlignment.Left));
                    row.Cells.Add(DCell($"{e.Amount:N2}", TextAlignment.Right));
                    row.Cells.Add(DCell(e.Note ?? string.Empty, TextAlignment.Left));
                    tg.Rows.Add(row);
                    i++;
                }

                doc.Blocks.Add(table);

                // Total row
                doc.Blocks.Add(new Paragraph(new Bold(new Run($"Total Expenses:   Rs. {TotalExpenses:N2}")))
                {
                    TextAlignment = TextAlignment.Right,
                    FontSize = 13,
                    Margin = new Thickness(0, 4, 0, 0),
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(0, 3, 0, 0)
                });

                // Printed-on footer
                doc.Blocks.Add(new Paragraph(new Run($"Printed: {DateTime.Now:dd-MMM-yyyy hh:mm tt}"))
                {
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    Margin = new Thickness(0, 6, 0, 0)
                });

                // Configure and print
                var printDialog = new PrintDialog();
                bool small = SettingsManager.LoadSettings().UseSmallBillFormat;
                if (small)
                {
                    doc.PageWidth = 280;
                    doc.PageHeight = double.NaN;
                    doc.ColumnWidth = 260;
                    doc.PagePadding = new Thickness(5);
                    doc.FontSize = 9;
                }
                else
                {
                    doc.PageWidth = printDialog.PrintableAreaWidth;
                    doc.PageHeight = printDialog.PrintableAreaHeight;
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(40);
                }

                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
                    $"Expense Report – {PeriodDescription}");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print expense report", ex.Message);
            }
        }

        private static TableCell HCell(string text)
        {
            var p = new Paragraph(new Bold(new Run(text))) { TextAlignment = TextAlignment.Center, Margin = new Thickness(0) };
            return new TableCell(p) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black, Padding = new Thickness(2, 1, 2, 1) };
        }

        private static TableCell DCell(string text, TextAlignment align)
        {
            var p = new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) };
            return new TableCell(p) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black, Padding = new Thickness(2, 1, 2, 1) };
        }
    }
}
