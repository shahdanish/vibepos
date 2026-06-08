using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class SalarySlipViewModel : ViewModelBase
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ISalarySlipRepository _salarySlipRepository;

        private Employee? _selectedEmployee;
        private int _selectedMonth = DateTime.Today.Month;
        private int _selectedYear = DateTime.Today.Year;
        private decimal _basicSalary;
        private decimal _houseRentAllowance;
        private decimal _medicalAllowance;
        private decimal _otherAllowances;
        private decimal _incomeTax;
        private decimal _eobiDeduction;
        private decimal _otherDeductions;
        private string _notes = string.Empty;
        private bool _isBusy;

        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<SalarySlip> EmployeeSlips { get; } = new();

        public IReadOnlyList<int> Months { get; } = Enumerable.Range(1, 12).ToList();
        public IReadOnlyList<int> Years { get; } = Enumerable.Range(DateTime.Today.Year - 5, 8).ToList();

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value))
                {
                    if (value != null)
                    {
                        BasicSalary = value.BasicSalary;
                        _ = LoadEmployeeSlipsAsync();
                    }
                    else
                    {
                        EmployeeSlips.Clear();
                    }
                }
            }
        }

        public int SelectedMonth
        {
            get => _selectedMonth;
            set => SetProperty(ref _selectedMonth, value);
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set => SetProperty(ref _selectedYear, value);
        }

        public decimal BasicSalary
        {
            get => _basicSalary;
            set { if (SetProperty(ref _basicSalary, value)) OnSalaryChanged(); }
        }

        public decimal HouseRentAllowance
        {
            get => _houseRentAllowance;
            set { if (SetProperty(ref _houseRentAllowance, value)) OnSalaryChanged(); }
        }

        public decimal MedicalAllowance
        {
            get => _medicalAllowance;
            set { if (SetProperty(ref _medicalAllowance, value)) OnSalaryChanged(); }
        }

        public decimal OtherAllowances
        {
            get => _otherAllowances;
            set { if (SetProperty(ref _otherAllowances, value)) OnSalaryChanged(); }
        }

        public decimal IncomeTax
        {
            get => _incomeTax;
            set { if (SetProperty(ref _incomeTax, value)) OnSalaryChanged(); }
        }

        public decimal EobiDeduction
        {
            get => _eobiDeduction;
            set { if (SetProperty(ref _eobiDeduction, value)) OnSalaryChanged(); }
        }

        public decimal OtherDeductions
        {
            get => _otherDeductions;
            set { if (SetProperty(ref _otherDeductions, value)) OnSalaryChanged(); }
        }

        public decimal GrossSalary => BasicSalary + HouseRentAllowance + MedicalAllowance + OtherAllowances;
        public decimal TotalDeductions => IncomeTax + EobiDeduction + OtherDeductions;
        public decimal NetSalary => GrossSalary - TotalDeductions;

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string MonthName(int month) => new DateTime(SelectedYear, month, 1).ToString("MMMM");

        public ICommand SaveAndPrintCommand { get; }
        public ICommand PrintExistingCommand { get; }
        public ICommand DeleteSlipCommand { get; }
        public ICommand ClearFormCommand { get; }

        public SalarySlipViewModel(IEmployeeRepository employeeRepository, ISalarySlipRepository salarySlipRepository)
        {
            _employeeRepository = employeeRepository;
            _salarySlipRepository = salarySlipRepository;

            SaveAndPrintCommand = new AsyncRelayCommand(SaveAndPrintAsync, () => !IsBusy);
            PrintExistingCommand = new RelayCommand(p => PrintSlip(p as SalarySlip), p => p is SalarySlip);
            DeleteSlipCommand = new RelayCommand(async p => await DeleteSlipAsync(p as SalarySlip), p => p is SalarySlip);
            ClearFormCommand = new RelayCommand(_ => ClearForm());

            _ = LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            var list = await _employeeRepository.GetAllAsync(includeInactive: false);
            Employees.Clear();
            foreach (var e in list)
                Employees.Add(e);
        }

        private async Task LoadEmployeeSlipsAsync()
        {
            if (SelectedEmployee == null) return;
            var slips = await _salarySlipRepository.GetByEmployeeAsync(SelectedEmployee.Id);
            EmployeeSlips.Clear();
            foreach (var s in slips)
                EmployeeSlips.Add(s);
        }

        private void OnSalaryChanged()
        {
            OnPropertyChanged(nameof(GrossSalary));
            OnPropertyChanged(nameof(TotalDeductions));
            OnPropertyChanged(nameof(NetSalary));
        }

        private async Task SaveAndPrintAsync()
        {
            if (SelectedEmployee == null)
            { NotificationHelper.ValidationError("Please select an employee."); return; }

            if (BasicSalary <= 0)
            { NotificationHelper.ValidationError("Basic salary must be greater than zero."); return; }

            if (await _salarySlipRepository.SlipExistsAsync(SelectedEmployee.Id, SelectedMonth, SelectedYear))
            {
                if (!NotificationHelper.Confirm(
                    $"A salary slip for {new DateTime(SelectedYear, SelectedMonth, 1):MMMM yyyy} already exists for {SelectedEmployee.Name}. Generate a new one anyway?",
                    "Duplicate Slip"))
                    return;
            }

            IsBusy = true;
            try
            {
                var slipNumber = await _salarySlipRepository.GenerateSlipNumberAsync();
                var slip = new SalarySlip
                {
                    SlipNumber = slipNumber,
                    EmployeeId = SelectedEmployee.Id,
                    Employee = SelectedEmployee,
                    Month = SelectedMonth,
                    Year = SelectedYear,
                    BasicSalary = BasicSalary,
                    HouseRentAllowance = HouseRentAllowance,
                    MedicalAllowance = MedicalAllowance,
                    OtherAllowances = OtherAllowances,
                    IncomeTax = IncomeTax,
                    EobiDeduction = EobiDeduction,
                    OtherDeductions = OtherDeductions,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    GeneratedDate = DateTime.Now,
                    GeneratedByUsername = SessionManager.CurrentUser?.Username
                };

                await _salarySlipRepository.AddAsync(slip);
                PrintSlip(slip);
                await LoadEmployeeSlipsAsync();
                NotificationHelper.ShowSuccess("Salary slip saved and sent to printer.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("generate salary slip", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteSlipAsync(SalarySlip? slip)
        {
            if (slip == null) return;
            if (!NotificationHelper.Confirm(
                    $"Delete salary slip {slip.SlipNumber} for {slip.MonthName}?",
                    "Delete Slip"))
                return;
            try
            {
                await _salarySlipRepository.SoftDeleteAsync(slip.Id);
                await LoadEmployeeSlipsAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete salary slip", ex.Message);
            }
        }

        private void ClearForm()
        {
            HouseRentAllowance = 0;
            MedicalAllowance = 0;
            OtherAllowances = 0;
            IncomeTax = 0;
            EobiDeduction = 0;
            OtherDeductions = 0;
            Notes = string.Empty;
            SelectedMonth = DateTime.Today.Month;
            SelectedYear = DateTime.Today.Year;
        }

        public void PrintSlip(SalarySlip? slip)
        {
            if (slip == null) return;

            // Resolve Employee navigation when loading from history (not eagerly included)
            if (slip.Employee == null)
            {
                var resolved = Employees.FirstOrDefault(e => e.Id == slip.EmployeeId) ?? _selectedEmployee;
                if (resolved != null) slip.Employee = resolved;
            }

            if (slip.Employee == null)
            {
                NotificationHelper.ValidationErrorCustom("Employee data not available for this slip. Please re-select the employee.");
                return;
            }

            try
            {
                var pd = new PrintDialog();
                double pageW = pd.PrintableAreaWidth  > 0 ? pd.PrintableAreaWidth  : 793.76;
                double pageH = pd.PrintableAreaHeight > 0 ? pd.PrintableAreaHeight : 1122.52;

                const double margin = 28.0;
                var slipVisual = BuildSalarySlipVisual(slip, pageW - 2 * margin);

                var page = new Border
                {
                    Width = pageW, Padding = new Thickness(margin),
                    Background = Brushes.White, Child = slipVisual
                };
                page.Measure(new Size(pageW, pageH));
                page.Arrange(new Rect(0, 0, pageW, pageH));
                page.UpdateLayout();

                pd.PrintVisual(page, $"Salary Slip – {slip.SlipNumber}");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print salary slip", ex.Message);
            }
        }

        // ── Visual builder (matches master_pharma_salary_slip.html design) ──────

        private static FrameworkElement BuildSalarySlipVisual(SalarySlip slip, double width)
        {
            var emp = slip.Employee;

            var darkNavy   = C(0x0d, 0x2b, 0x52);
            var midNavy    = C(0x1a, 0x4a, 0x7a);
            var lightBg    = C(0xee, 0xf3, 0xf9);
            var blueBorder = C(0xc2, 0xd5, 0xeb);
            var navyMuted  = C(0xa8, 0xc4, 0xe0);
            var textPri    = C(0x1a, 0x1a, 0x1a);
            var textSec    = C(0x6b, 0x72, 0x80);
            var lineColor  = C(0xe5, 0xe7, 0xeb);

            var root  = new Border { Width = width, Background = Brushes.White, BorderBrush = blueBorder, BorderThickness = new Thickness(1) };
            var outer = new StackPanel();
            root.Child = outer;

            outer.Children.Add(SlipHeader(slip, emp, darkNavy, midNavy, navyMuted));
            outer.Children.Add(SlipNoBar(slip, midNavy, navyMuted));

            var body      = new Border { Padding = new Thickness(20, 16, 20, 16) };
            var bodyStack = new StackPanel();
            body.Child    = bodyStack;

            bodyStack.Children.Add(SectionTitle("Employee Information", midNavy));
            bodyStack.Children.Add(EmpInfoGrid(emp, slip, textPri, textSec, lineColor));
            bodyStack.Children.Add(SalarySection(slip, lightBg, midNavy, textPri, lineColor));
            bodyStack.Children.Add(TotalsRow(slip));
            bodyStack.Children.Add(NetWordsBar(slip, darkNavy, navyMuted));

            if (!string.IsNullOrWhiteSpace(slip.Notes))
            {
                bodyStack.Children.Add(SectionTitle("Notes", midNavy));
                bodyStack.Children.Add(new Border
                {
                    BorderBrush = lineColor, BorderThickness = new Thickness(0.5),
                    CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 14),
                    Child = new TextBlock { Text = slip.Notes, FontSize = 11, Foreground = textSec, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap }
                });
            }

            bodyStack.Children.Add(SignatureSection(midNavy, textPri, textSec, lineColor));
            outer.Children.Add(body);
            outer.Children.Add(new Border
            {
                Background = lightBg, BorderBrush = blueBorder, BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 7, 20, 7),
                Child = new TextBlock
                {
                    Text = "This is a computer-generated salary slip and is valid without a physical signature unless required by company policy.",
                    FontSize = 9.5, Foreground = midNavy, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap
                }
            });

            return root;
        }

        private static UIElement SlipHeader(SalarySlip slip, Employee emp,
            SolidColorBrush darkNavy, SolidColorBrush midNavy, SolidColorBrush navyMuted)
        {
            var header = new Border { Background = darkNavy, Padding = new Thickness(20, 16, 20, 14) };
            var grid   = TwoColGrid(star: true);

            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = "Master Pharmaceuticals", Foreground = Brushes.White, FontSize = 17, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"), Margin = new Thickness(0, 0, 0, 3) });
            left.Children.Add(new TextBlock { Text = "Distributor  |  Office #410, 4th Floor, Kohistan Tower Saddar, Rawalpindi", Foreground = navyMuted, FontSize = 10, TextWrapping = TextWrapping.Wrap });
            left.Children.Add(new TextBlock { Text = "NTN# G985456  |  DSL# 374-89100937-2025", Foreground = navyMuted, FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(left, 0);

            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock { Text = "Salary Slip", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 2) });
            right.Children.Add(new TextBlock { Text = "Confidential Document", Foreground = navyMuted, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6) });
            right.Children.Add(new Border
            {
                Background = midNavy, CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock { Text = slip.MonthName, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold }
            });
            Grid.SetColumn(right, 1);

            grid.Children.Add(left);
            grid.Children.Add(right);
            header.Child = grid;
            return header;
        }

        private static UIElement SlipNoBar(SalarySlip slip, SolidColorBrush midNavy, SolidColorBrush navyMuted)
        {
            var bar  = new Border { Background = midNavy, Padding = new Thickness(20, 6, 20, 6) };
            var grid = new Grid();
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var items = new[]
            {
                (Label: "Slip No: ",       Value: slip.SlipNumber,                      Align: HorizontalAlignment.Left),
                (Label: "Generated: ",     Value: slip.GeneratedDate.ToString("dd-MMM-yyyy"), Align: HorizontalAlignment.Center),
                (Label: "Payment Mode: ",  Value: "Bank Transfer",                      Align: HorizontalAlignment.Right),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = items[i].Align };
                sp.Children.Add(new TextBlock { Text = items[i].Label, Foreground = navyMuted, FontSize = 11 });
                sp.Children.Add(new TextBlock { Text = items[i].Value, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold });
                Grid.SetColumn(sp, i);
                grid.Children.Add(sp);
            }

            bar.Child = grid;
            return bar;
        }

        private static UIElement EmpInfoGrid(Employee emp, SalarySlip slip,
            SolidColorBrush textPri, SolidColorBrush textSec, SolidColorBrush line)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var leftRows  = new[] { ("Employee Name", emp.Name), ("Designation", emp.Designation ?? "—"), ("Employee ID", emp.EmployeeCode) };
            var rightRows = new[] { ("Department", emp.Department ?? "—"), ("Salary Month", slip.MonthName), ("Days Worked", "30 / 30") };

            for (int r = 0; r < 3; r++)
            {
                AddInfoCell(grid, leftRows[r].Item1,  leftRows[r].Item2,  r, 0, textPri, textSec, line);
                AddInfoCell(grid, rightRows[r].Item1, rightRows[r].Item2, r, 2, textPri, textSec, line);
            }
            return grid;
        }

        private static void AddInfoCell(Grid grid, string label, string value,
            int row, int col, SolidColorBrush textPri, SolidColorBrush textSec, SolidColorBrush line)
        {
            var inner = TwoColGrid(star: false);
            inner.Children.Add(new TextBlock { Text = label, FontSize = 11.5, Foreground = textSec });
            var val = new TextBlock { Text = value, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = textPri, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(val, 1);
            inner.Children.Add(val);

            var cell = new Border { BorderBrush = line, BorderThickness = new Thickness(0, 0, 0, 0.5), Padding = new Thickness(0, 4, 0, 4), Child = inner };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            grid.Children.Add(cell);
        }

        private static UIElement SalarySection(SalarySlip slip,
            SolidColorBrush hdrBg, SolidColorBrush hdrFg, SolidColorBrush textPri, SolidColorBrush line)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var earnStack = new StackPanel();
            earnStack.Children.Add(SectionTitle("Earnings", hdrFg));
            earnStack.Children.Add(SalaryTable(new[] { ("Basic Salary", slip.BasicSalary), ("House Rent Allow.", slip.HouseRentAllowance), ("Medical Allowance", slip.MedicalAllowance), ("Other Allowances", slip.OtherAllowances) }, hdrBg, hdrFg, textPri, line));
            Grid.SetColumn(earnStack, 0);

            var dedStack = new StackPanel();
            dedStack.Children.Add(SectionTitle("Deductions", hdrFg));
            dedStack.Children.Add(SalaryTable(new[] { ("Income Tax", slip.IncomeTax), ("EOBI", slip.EobiDeduction), ("Other Deductions", slip.OtherDeductions), ("—", 0m) }, hdrBg, hdrFg, textPri, line));
            Grid.SetColumn(dedStack, 2);

            grid.Children.Add(earnStack);
            grid.Children.Add(dedStack);
            return grid;
        }

        private static UIElement SalaryTable((string name, decimal amount)[] rows,
            SolidColorBrush hdrBg, SolidColorBrush hdrFg, SolidColorBrush textPri, SolidColorBrush line)
        {
            var stack = new StackPanel();

            var hdr = new Grid { Background = hdrBg };
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
            hdr.Children.Add(new TextBlock { Text = "Component",    FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = hdrFg, Padding = new Thickness(8, 6, 8, 6) });
            var ah = new TextBlock { Text = "Amount (Rs.)", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = hdrFg, TextAlignment = TextAlignment.Right, Padding = new Thickness(8, 6, 8, 6) };
            Grid.SetColumn(ah, 1);
            hdr.Children.Add(ah);
            stack.Children.Add(hdr);

            foreach (var (name, amount) in rows)
            {
                var rg = new Grid { Background = Brushes.White };
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
                rg.Children.Add(new TextBlock { Text = name, FontSize = 11.5, Foreground = textPri, Padding = new Thickness(8, 6, 8, 6) });
                var ac = new TextBlock
                {
                    Text = name == "—" ? "—" : $"Rs. {amount:N2}",
                    FontSize = 11.5, FontWeight = FontWeights.Medium,
                    Foreground = textPri, TextAlignment = TextAlignment.Right,
                    Padding = new Thickness(8, 6, 8, 6)
                };
                Grid.SetColumn(ac, 1);
                rg.Children.Add(ac);
                stack.Children.Add(new Border { BorderBrush = line, BorderThickness = new Thickness(0, 0, 0, 0.5), Child = rg });
            }

            return new Border { BorderBrush = line, BorderThickness = new Thickness(0.5), Child = stack };
        }

        private static UIElement TotalsRow(SalarySlip slip)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < 5; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i % 2 == 1 ? new GridLength(8) : new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var gross = TotalBox("Gross Salary",      slip.GrossSalary,    C(0xee,0xf3,0xf9), C(0x1a,0x4a,0x7a), C(0x0c,0x44,0x7c));
            var ded   = TotalBox("Total Deductions",  slip.TotalDeductions, C(0xfc,0xeb,0xeb), C(0xa3,0x2d,0x2d), C(0x79,0x1f,0x1f));
            var net   = TotalBox("Net Salary",        slip.NetSalary,       C(0xe1,0xf5,0xee), C(0x0f,0x6e,0x56), C(0x08,0x50,0x41));

            Grid.SetColumn(gross, 0); Grid.SetColumn(ded, 2); Grid.SetColumn(net, 4);
            grid.Children.Add(gross); grid.Children.Add(ded); grid.Children.Add(net);
            return grid;
        }

        private static Border TotalBox(string label, decimal amount,
            SolidColorBrush bg, SolidColorBrush border, SolidColorBrush amtColor)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = label.ToUpper(), FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = border, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3) });
            stack.Children.Add(new TextBlock { Text = $"Rs. {amount:N0}", FontSize = 15, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Georgia"), Foreground = amtColor, HorizontalAlignment = HorizontalAlignment.Center });
            return new Border { Background = bg, BorderBrush = border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 9, 10, 9), Child = stack };
        }

        private static UIElement NetWordsBar(SalarySlip slip, SolidColorBrush darkNavy, SolidColorBrush navyMuted)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = "Net Salary in Words: ", Foreground = navyMuted, FontSize = 11 });
            sp.Children.Add(new TextBlock { Text = AmountInWords(slip.NetSalary), Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold });
            return new Border { Background = darkNavy, CornerRadius = new CornerRadius(3), Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 0, 14), Child = sp };
        }

        private static UIElement SignatureSection(SolidColorBrush midNavy,
            SolidColorBrush textPri, SolidColorBrush textSec, SolidColorBrush line)
        {
            var outer = new Border { BorderBrush = line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 14, 0, 0) };
            var stack = new StackPanel();
            stack.Children.Add(SectionTitle("Authorized Signatures", midNavy));

            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string[] titles = { "Chief Executive Officer", "Director General", "General Manager" };
            for (int i = 0; i < 3; i++)
            {
                var box = new StackPanel { Margin = new Thickness(16, 0, 16, 0) };
                box.Children.Add(new Border { Height = 44 });
                box.Children.Add(new Border
                {
                    BorderBrush = C(0x6b, 0x72, 0x80), BorderThickness = new Thickness(0, 1, 0, 0),
                    Padding = new Thickness(0, 5, 0, 0),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = titles[i], FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = textPri, HorizontalAlignment = HorizontalAlignment.Center },
                            new TextBlock { Text = "Master Pharmaceuticals", FontSize = 10, Foreground = textSec, HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    }
                });
                Grid.SetColumn(box, i);
                grid.Children.Add(box);
            }

            stack.Children.Add(grid);
            outer.Child = stack;
            return outer;
        }

        private static UIElement SectionTitle(string text, SolidColorBrush color) =>
            new Border
            {
                BorderBrush = color, BorderThickness = new Thickness(0, 0, 0, 1.5),
                Padding = new Thickness(0, 0, 0, 4), Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock { Text = text.ToUpper(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = color }
            };

        private static Grid TwoColGrid(bool star)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = star ? new GridLength(1, GridUnitType.Star) : new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = star ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
            return g;
        }

        private static SolidColorBrush C(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

        // ── Number-to-words (Pakistani: Lakh / Crore) ──────────────────────────

        private static string AmountInWords(decimal amount)
        {
            var n = (long)Math.Floor(Math.Abs(amount));
            return $"{N2W(n)} Rupees Only";
        }

        private static readonly string[] _ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] _tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        private static string N2W(long n)
        {
            if (n == 0)        return "Zero";
            if (n < 20)        return _ones[n];
            if (n < 100)       return _tens[n / 10] + (n % 10 > 0 ? " " + _ones[n % 10] : "");
            if (n < 1_000)     return _ones[n / 100] + " Hundred" + (n % 100 > 0 ? " " + N2W(n % 100) : "");
            if (n < 100_000)   return N2W(n / 1_000) + " Thousand" + (n % 1_000 > 0 ? " " + N2W(n % 1_000) : "");
            if (n < 10_000_000) return N2W(n / 100_000) + " Lakh" + (n % 100_000 > 0 ? " " + N2W(n % 100_000) : "");
            return N2W(n / 10_000_000) + " Crore" + (n % 10_000_000 > 0 ? " " + N2W(n % 10_000_000) : "");
        }

    }
}
/*

            // ── Company Header ────────────────────────────────────────────────
            var header = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            header.Inlines.Add(new Run("Master Pharmaceuticals Distributor")
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            doc.Blocks.Add(header);

            var subHeader = new Paragraph
            {
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            subHeader.Inlines.Add(new Run("SALARY SLIP") { FontSize = 13, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(subHeader);

            // ── Divider ───────────────────────────────────────────────────────
            doc.Blocks.Add(HrLine());

            // ── Slip info row ─────────────────────────────────────────────────
            var infoTable = new Table { CellSpacing = 0 };
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var infoGroup = new TableRowGroup();
            infoTable.RowGroups.Add(infoGroup);
            var infoRow = new TableRow();
            infoGroup.Rows.Add(infoRow);
            infoRow.Cells.Add(TableCell($"Slip No: {slip.SlipNumber}", TextAlignment.Left));
            infoRow.Cells.Add(TableCell($"Month: {slip.MonthName}", TextAlignment.Right));
            doc.Blocks.Add(infoTable);

            var generatedRow = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
            generatedRow.Inlines.Add(new Run($"Generated: {slip.GeneratedDate:dd-MMM-yyyy}  |  By: {slip.GeneratedByUsername ?? "—"}")
            { FontSize = 9, Foreground = Brushes.Gray });
            doc.Blocks.Add(generatedRow);

            // ── Employee Details ──────────────────────────────────────────────
            doc.Blocks.Add(SectionHeader("Employee Information"));

            var empTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
            empTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
            empTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            empTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
            empTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var empGroup = new TableRowGroup();
            empTable.RowGroups.Add(empGroup);

            void AddEmpRow(string l1, string v1, string l2, string v2)
            {
                var r = new TableRow();
                r.Cells.Add(LabelCell(l1));
                r.Cells.Add(ValueCell(v1));
                r.Cells.Add(LabelCell(l2));
                r.Cells.Add(ValueCell(v2));
                empGroup.Rows.Add(r);
            }

            var emp = slip.Employee;
            AddEmpRow("Employee Name:", emp.Name, "Employee Code:", emp.EmployeeCode);
            AddEmpRow("Father Name:", emp.FatherName ?? "—", "CNIC:", emp.Cnic ?? "—");
            AddEmpRow("Designation:", emp.Designation, "Department:", emp.Department ?? "—");
            AddEmpRow("Cell Number:", emp.CellNumber ?? "—", "Joining Date:", emp.JoiningDate.ToString("dd-MMM-yyyy"));
            doc.Blocks.Add(empTable);

            // ── Salary Details ────────────────────────────────────────────────
            doc.Blocks.Add(SectionHeader("Salary Details"));

            var salTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 10) };
            salTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            salTable.Columns.Add(new TableColumn { Width = new GridLength(120) });
            salTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            salTable.Columns.Add(new TableColumn { Width = new GridLength(120) });
            var salGroup = new TableRowGroup();
            salTable.RowGroups.Add(salGroup);

            // Header row
            var salHeaderRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x6b, 0x9a)) };
            salHeaderRow.Cells.Add(ColHeaderCell("Earnings"));
            salHeaderRow.Cells.Add(ColHeaderCell("Amount (Rs.)"));
            salHeaderRow.Cells.Add(ColHeaderCell("Deductions"));
            salHeaderRow.Cells.Add(ColHeaderCell("Amount (Rs.)"));
            salGroup.Rows.Add(salHeaderRow);

            void AddSalRow(string earn, decimal earnAmt, string ded, decimal dedAmt)
            {
                var r = new TableRow();
                r.Cells.Add(LabelCell(earn));
                r.Cells.Add(AmountCell(earnAmt));
                r.Cells.Add(LabelCell(ded));
                r.Cells.Add(AmountCell(dedAmt));
                salGroup.Rows.Add(r);
            }

            AddSalRow("Basic Salary", slip.BasicSalary, "Income Tax", slip.IncomeTax);
            AddSalRow("House Rent Allowance", slip.HouseRentAllowance, "EOBI", slip.EobiDeduction);
            AddSalRow("Medical Allowance", slip.MedicalAllowance, "Other Deductions", slip.OtherDeductions);
            AddSalRow("Other Allowances", slip.OtherAllowances, string.Empty, 0m);

            // Totals row
            var totalsRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf0, 0xf0)) };
            totalsRow.Cells.Add(BoldCell("Gross Salary"));
            totalsRow.Cells.Add(BoldAmountCell(slip.GrossSalary));
            totalsRow.Cells.Add(BoldCell("Total Deductions"));
            totalsRow.Cells.Add(BoldAmountCell(slip.TotalDeductions));
            salGroup.Rows.Add(totalsRow);

            doc.Blocks.Add(salTable);

            // ── Net Salary ────────────────────────────────────────────────────
            doc.Blocks.Add(BuildNetSalaryBandBlock(slip.NetSalary));

            // ── Notes ─────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(slip.Notes))
            {
                var notePara = new Paragraph { Margin = new Thickness(0, 0, 0, 12) };
                notePara.Inlines.Add(new Run("Note: ") { FontWeight = FontWeights.Bold });
                notePara.Inlines.Add(new Run(slip.Notes));
                doc.Blocks.Add(notePara);
            }

            doc.Blocks.Add(HrLine());

            // ── Signature Section ─────────────────────────────────────────────
            doc.Blocks.Add(SectionHeader("Authorized Signatures"));
            doc.Blocks.Add(BuildSignatureBlock());

            return doc;
        }

        private static Table BuildNetSalaryBandBlock(decimal netSalary)
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 12) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var group = new TableRowGroup();
            table.RowGroups.Add(group);
            var row = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x6b, 0x9a)) };
            row.Cells.Add(new TableCell(
                new Paragraph(new Run("NET SALARY PAYABLE") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White }))
                { Padding = new Thickness(12, 8, 12, 8) });
            row.Cells.Add(new TableCell(
                new Paragraph(new Run($"Rs. {netSalary:N2}") { FontSize = 15, FontWeight = FontWeights.Bold, Foreground = Brushes.White })
                { TextAlignment = TextAlignment.Right })
                { Padding = new Thickness(12, 8, 12, 8) });
            group.Rows.Add(row);
            return table;
        }

        private static Table BuildSignatureBlock()
        {
            var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 16, 0, 0) };
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            string[] titles = { "Chief Executive Officer", "Director General", "General Manager" };

            // Blank signature line row (underscores simulate the signature line)
            var lineRow = new TableRow();
            foreach (var _ in titles)
            {
                lineRow.Cells.Add(new TableCell(
                    new Paragraph(new Run(new string('_', 28)))
                    { TextAlignment = TextAlignment.Center, Foreground = Brushes.Black })
                    { Padding = new Thickness(16, 40, 16, 2) });
            }
            group.Rows.Add(lineRow);

            // Designation label row
            var titleRow = new TableRow();
            foreach (var title in titles)
            {
                titleRow.Cells.Add(new TableCell(
                    new Paragraph(new Run(title) { FontWeight = FontWeights.SemiBold })
                    { TextAlignment = TextAlignment.Center })
                    { Padding = new Thickness(8, 2, 8, 8) });
            }
            group.Rows.Add(titleRow);

            return table;
        }

        // ── Document helpers ──────────────────────────────────────────────────

        private static Paragraph HrLine() =>
            new Paragraph(new Run(new string('─', 90))) { FontFamily = new FontFamily("Courier New"), FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(0, 4, 0, 4) };

        private static Paragraph SectionHeader(string text) =>
            new Paragraph(new Run(text) { FontWeight = FontWeights.Bold, FontSize = 11 })
            { Background = new SolidColorBrush(Color.FromRgb(0xe8, 0xf4, 0xfd)), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 6, 0, 4) };

        private static TableCell TableCell(string text, TextAlignment align) =>
            new TableCell(new Paragraph(new Run(text)) { TextAlignment = align }) { Padding = new Thickness(4, 2, 4, 2) };

        private static TableCell LabelCell(string text) =>
            new TableCell(new Paragraph(new Run(text) { Foreground = Brushes.Gray })) { Padding = new Thickness(4, 2, 4, 2) };

        private static TableCell ValueCell(string text) =>
            new TableCell(new Paragraph(new Run(text) { FontWeight = FontWeights.SemiBold })) { Padding = new Thickness(4, 2, 4, 2) };

        private static TableCell ColHeaderCell(string text) =>
            new TableCell(new Paragraph(new Run(text) { Foreground = Brushes.White, FontWeight = FontWeights.Bold }))
            { Padding = new Thickness(6, 4, 6, 4) };

        private static TableCell AmountCell(decimal amount)
        {
            var text = amount == 0 ? "—" : $"Rs. {amount:N2}";
            return new TableCell(new Paragraph(new Run(text)) { TextAlignment = TextAlignment.Right })
            { Padding = new Thickness(4, 2, 6, 2) };
        }

        private static TableCell BoldCell(string text) =>
            new TableCell(new Paragraph(new Run(text) { FontWeight = FontWeights.Bold }))
            { Padding = new Thickness(6, 3, 6, 3) };

        private static TableCell BoldAmountCell(decimal amount) =>
            new TableCell(new Paragraph(new Run($"Rs. {amount:N2}") { FontWeight = FontWeights.Bold }) { TextAlignment = TextAlignment.Right })
            { Padding = new Thickness(4, 3, 6, 3) };
*/
