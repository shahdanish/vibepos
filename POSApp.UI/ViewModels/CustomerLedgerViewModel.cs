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
    public enum LedgerDateFilter { All, Today, ThisWeek, ThisMonth, Custom }

    public sealed class CustomerLedgerViewModel : ViewModelBase
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerPaymentRepository _paymentRepository;

        private Customer? _selectedCustomer;
        private decimal _paymentAmount;
        private string? _paymentNote;
        private string? _paymentInvoiceNumber;

        // Customer search + edit state
        private List<Customer> _allCustomers = new();
        private string _customerSearchText = string.Empty;
        private Customer? _editingCustomer;       // non-null = editor is updating this customer

        // Payment edit state
        private CustomerPayment? _selectedPayment;
        private CustomerPayment? _editingPayment;  // non-null = payment form is editing this payment

        // Date filter state
        private LedgerDateFilter _activeFilter = LedgerDateFilter.All;
        private DateTime _filterStartDate = DateTime.Today;
        private DateTime _filterEndDate = DateTime.Today;
        private List<CustomerPayment> _allPayments = new();

        // Add customer fields
        private string _newCustomerName = string.Empty;
        private string? _newCustomerPhone;
        private string? _newCustomerAddress;
        private decimal _newCustomerInitialBalance;

        // Last payment tracking
        private CustomerPayment? _lastPayment;
        private Customer? _lastPaymentCustomer;
        private decimal _balanceBeforeLastPayment;

        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<CustomerPayment> Payments { get; } = new();

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    LoadCustomerIntoEditor(value);
                    _ = LoadPaymentsSafe();
                }
            }
        }

        public string CustomerSearchText
        {
            get => _customerSearchText;
            set
            {
                if (SetProperty(ref _customerSearchText, value))
                    ApplyCustomerSearch();
            }
        }

        public CustomerPayment? SelectedPayment
        {
            get => _selectedPayment;
            set => SetProperty(ref _selectedPayment, value);
        }

        // Editor mode labels
        public bool IsEditingCustomer => _editingCustomer != null;
        public bool IsNewCustomer => _editingCustomer == null;
        public string CustomerFormTitle => _editingCustomer != null ? "Edit Customer" : "Add New Customer";
        public string SaveCustomerButtonText => _editingCustomer != null ? "💾 Update Customer" : "➕ Add Customer";

        public bool IsEditingPayment => _editingPayment != null;
        public string SavePaymentButtonText => _editingPayment != null ? "💾 Update Payment" : "✅ Record Payment";

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set => SetProperty(ref _paymentAmount, value);
        }

        public string? PaymentNote
        {
            get => _paymentNote;
            set => SetProperty(ref _paymentNote, value);
        }

        public string? PaymentInvoiceNumber
        {
            get => _paymentInvoiceNumber;
            set => SetProperty(ref _paymentInvoiceNumber, value);
        }

        // Date filter
        public LedgerDateFilter ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    OnPropertyChanged(nameof(IsCustomRange));
                    ApplyFilter();
                }
            }
        }

        public bool IsCustomRange => _activeFilter == LedgerDateFilter.Custom;

        public DateTime FilterStartDate
        {
            get => _filterStartDate;
            set
            {
                if (SetProperty(ref _filterStartDate, value) && _activeFilter == LedgerDateFilter.Custom)
                    ApplyFilter();
            }
        }

        public DateTime FilterEndDate
        {
            get => _filterEndDate;
            set
            {
                if (SetProperty(ref _filterEndDate, value) && _activeFilter == LedgerDateFilter.Custom)
                    ApplyFilter();
            }
        }

        public string NewCustomerName
        {
            get => _newCustomerName;
            set => SetProperty(ref _newCustomerName, value);
        }

        public string? NewCustomerPhone
        {
            get => _newCustomerPhone;
            set => SetProperty(ref _newCustomerPhone, value);
        }

        public string? NewCustomerAddress
        {
            get => _newCustomerAddress;
            set => SetProperty(ref _newCustomerAddress, value);
        }

        public decimal NewCustomerInitialBalance
        {
            get => _newCustomerInitialBalance;
            set => SetProperty(ref _newCustomerInitialBalance, value);
        }

        public ICommand AddPaymentCommand { get; }      // also handles updates (Save)
        public ICommand DeletePaymentCommand { get; }
        public ICommand EditPaymentCommand { get; }
        public ICommand CancelPaymentEditCommand { get; }
        public ICommand SaveCustomerCommand { get; }    // add or update
        public ICommand DeleteCustomerCommand { get; }
        public ICommand NewCustomerCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand FilterAllCommand { get; }
        public ICommand FilterTodayCommand { get; }
        public ICommand FilterWeekCommand { get; }
        public ICommand FilterMonthCommand { get; }
        public ICommand FilterCustomCommand { get; }

        public CustomerLedgerViewModel(ICustomerRepository customerRepository, ICustomerPaymentRepository paymentRepository)
        {
            _customerRepository = customerRepository;
            _paymentRepository = paymentRepository;

            AddPaymentCommand        = new RelayCommand(async _ => await SavePayment());
            DeletePaymentCommand     = new RelayCommand(async p => await DeletePayment(p));
            EditPaymentCommand       = new RelayCommand(p => BeginEditPayment(p));
            CancelPaymentEditCommand = new RelayCommand(_ => ClearPaymentForm());
            SaveCustomerCommand      = new RelayCommand(async _ => await SaveCustomer());
            DeleteCustomerCommand    = new RelayCommand(async _ => await DeleteCustomer(), _ => _selectedCustomer != null);
            NewCustomerCommand       = new RelayCommand(_ => StartNewCustomer());
            RefreshCommand           = new RelayCommand(async _ => await LoadCustomers());
            PrintReceiptCommand  = new RelayCommand(_ => ReprintLastReceipt(), _ => _lastPayment != null);
            FilterAllCommand     = new RelayCommand(_ => ActiveFilter = LedgerDateFilter.All);
            FilterTodayCommand   = new RelayCommand(_ => ActiveFilter = LedgerDateFilter.Today);
            FilterWeekCommand    = new RelayCommand(_ => ActiveFilter = LedgerDateFilter.ThisWeek);
            FilterMonthCommand   = new RelayCommand(_ => ActiveFilter = LedgerDateFilter.ThisMonth);
            FilterCustomCommand  = new RelayCommand(_ => ActiveFilter = LedgerDateFilter.Custom);

            _ = LoadCustomersSafe();
        }

        private async Task LoadCustomersSafe()
        {
            try { await LoadCustomers(); }
            catch (Exception ex) { NotificationHelper.OperationFailed("load customers", ex.Message); }
        }

        private async Task LoadCustomers()
        {
            var customers = await _customerRepository.GetAllAsync();
            _allCustomers = customers.OrderByDescending(c => c.CurrentBalance).ToList();
            ApplyCustomerSearch();
        }

        private void ApplyCustomerSearch()
        {
            IEnumerable<Customer> filtered = _allCustomers;

            if (!string.IsNullOrWhiteSpace(_customerSearchText))
            {
                var term = _customerSearchText.Trim();
                filtered = filtered.Where(c =>
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.Phone != null && c.Phone.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (c.CustomerId != null && c.CustomerId.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            Customers.Clear();
            foreach (var customer in filtered)
                Customers.Add(customer);
        }

        // ── Customer editor ───────────────────────────────────────────────────

        private void LoadCustomerIntoEditor(Customer? customer)
        {
            _editingCustomer = customer;
            if (customer != null)
            {
                _newCustomerName = customer.Name;
                _newCustomerPhone = customer.Phone;
                _newCustomerAddress = customer.Address;
                _newCustomerInitialBalance = customer.CurrentBalance;
            }
            else
            {
                _newCustomerName = string.Empty;
                _newCustomerPhone = null;
                _newCustomerAddress = null;
                _newCustomerInitialBalance = 0;
            }

            OnPropertyChanged(nameof(NewCustomerName));
            OnPropertyChanged(nameof(NewCustomerPhone));
            OnPropertyChanged(nameof(NewCustomerAddress));
            OnPropertyChanged(nameof(NewCustomerInitialBalance));
            OnPropertyChanged(nameof(IsEditingCustomer));
            OnPropertyChanged(nameof(IsNewCustomer));
            OnPropertyChanged(nameof(CustomerFormTitle));
            OnPropertyChanged(nameof(SaveCustomerButtonText));
        }

        private void StartNewCustomer()
        {
            SelectedCustomer = null;   // triggers LoadCustomerIntoEditor(null)
        }

        private async Task SaveCustomer()
        {
            if (_editingCustomer != null)
                await UpdateCustomer();
            else
                await AddCustomer();
        }

        private async Task UpdateCustomer()
        {
            if (_editingCustomer == null) return;

            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                NotificationHelper.ValidationErrorCustom("Please enter the customer name.");
                return;
            }

            try
            {
                _editingCustomer.Name = NewCustomerName.Trim();
                _editingCustomer.Phone = NewCustomerPhone;
                _editingCustomer.CellNo = NewCustomerPhone;
                _editingCustomer.Address = NewCustomerAddress;
                _editingCustomer.ModifiedDate = DateTime.Now;

                await _customerRepository.UpdateAsync(_editingCustomer);

                int id = _editingCustomer.Id;
                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);

                NotificationHelper.ShowSuccess("Customer updated.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update customer", ex.Message);
            }
        }

        private async Task DeleteCustomer()
        {
            if (SelectedCustomer == null) return;

            if (!NotificationHelper.Confirm(
                $"Delete customer '{SelectedCustomer.Name}'?\nThis also removes their payment history. This cannot be undone."))
                return;

            try
            {
                await _customerRepository.DeleteAsync(SelectedCustomer.Id);
                SelectedCustomer = null;
                await LoadCustomers();
                NotificationHelper.ShowSuccess("Customer deleted.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete customer", ex.Message);
            }
        }

        private async Task LoadPaymentsSafe()
        {
            try { await LoadPayments(); }
            catch (Exception ex) { NotificationHelper.OperationFailed("load payment history", ex.Message); }
        }

        private async Task LoadPayments()
        {
            if (SelectedCustomer == null)
            {
                _allPayments.Clear();
                Payments.Clear();
                return;
            }
            var payments = await _paymentRepository.GetByCustomerIdAsync(SelectedCustomer.Id);
            _allPayments = payments.OrderByDescending(p => p.PaymentDate).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<CustomerPayment> filtered = _allPayments;

            filtered = _activeFilter switch
            {
                LedgerDateFilter.Today     => filtered.Where(p => p.PaymentDate.Date == DateTime.Today),
                LedgerDateFilter.ThisWeek  => filtered.Where(p => p.PaymentDate.Date >= DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek)),
                LedgerDateFilter.ThisMonth => filtered.Where(p => p.PaymentDate.Year == DateTime.Today.Year && p.PaymentDate.Month == DateTime.Today.Month),
                LedgerDateFilter.Custom    => filtered.Where(p => p.PaymentDate.Date >= _filterStartDate.Date && p.PaymentDate.Date <= _filterEndDate.Date),
                _                          => filtered
            };

            Payments.Clear();
            foreach (var p in filtered)
                Payments.Add(p);
        }

        private async Task AddCustomer()
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                NotificationHelper.ValidationErrorCustom("Please enter the customer name.");
                return;
            }

            try
            {
                var all = await _customerRepository.GetAllAsync();
                var maxNum = all
                    .Select(c => { int.TryParse(c.CustomerId.TrimStart('C'), out int n); return n; })
                    .DefaultIfEmpty(0).Max();
                var customerId = $"C{(maxNum + 1):D4}";

                var customer = new Customer
                {
                    CustomerId = customerId,
                    Name = NewCustomerName.Trim(),
                    Phone = NewCustomerPhone,
                    CellNo = NewCustomerPhone,
                    Address = NewCustomerAddress,
                    PreBalance = NewCustomerInitialBalance,
                    CurrentBalance = NewCustomerInitialBalance,
                    CreatedDate = DateTime.Now
                };

                await _customerRepository.AddAsync(customer);

                NewCustomerName = string.Empty;
                NewCustomerPhone = null;
                NewCustomerAddress = null;
                NewCustomerInitialBalance = 0;

                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == customerId);

                NotificationHelper.ShowSuccess($"Customer '{customer.Name}' added!");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("add customer", ex.Message);
            }
        }

        // ── Payment add / edit / delete ───────────────────────────────────────

        private async Task SavePayment()
        {
            if (_editingPayment != null)
                await UpdatePayment();
            else
                await AddPayment();
        }

        private void BeginEditPayment(object? param)
        {
            if (param is not CustomerPayment payment) return;

            _editingPayment = payment;
            PaymentAmount = payment.AmountPaid;
            PaymentInvoiceNumber = payment.InvoiceNumber;
            PaymentNote = payment.Note;

            OnPropertyChanged(nameof(IsEditingPayment));
            OnPropertyChanged(nameof(SavePaymentButtonText));
        }

        private void ClearPaymentForm()
        {
            _editingPayment = null;
            PaymentAmount = 0;
            PaymentNote = null;
            PaymentInvoiceNumber = null;

            OnPropertyChanged(nameof(IsEditingPayment));
            OnPropertyChanged(nameof(SavePaymentButtonText));
        }

        private async Task UpdatePayment()
        {
            if (_editingPayment == null) return;

            if (PaymentAmount <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Please enter a valid payment amount.");
                return;
            }

            try
            {
                _editingPayment.AmountPaid = PaymentAmount;
                _editingPayment.InvoiceNumber = PaymentInvoiceNumber?.Trim();
                _editingPayment.Note = PaymentNote;

                await _paymentRepository.UpdateAsync(_editingPayment);

                int? customerId = SelectedCustomer?.Id;
                ClearPaymentForm();
                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == customerId);

                NotificationHelper.ShowSuccess("Payment updated.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update payment", ex.Message);
            }
        }

        private async Task DeletePayment(object? param)
        {
            if (param is not CustomerPayment payment) return;

            if (!NotificationHelper.Confirm(
                $"Delete this payment of Rs.{payment.AmountPaid:N2}?\nThe amount will be added back to the customer's balance."))
                return;

            try
            {
                int? customerId = SelectedCustomer?.Id;
                await _paymentRepository.DeleteAsync(payment.Id);

                if (_editingPayment?.Id == payment.Id)
                    ClearPaymentForm();

                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == customerId);

                NotificationHelper.ShowSuccess("Payment deleted.");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("delete payment", ex.Message);
            }
        }

        private async Task AddPayment()
        {
            if (SelectedCustomer == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a customer.");
                return;
            }

            if (PaymentAmount <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Please enter a valid payment amount.");
                return;
            }

            try
            {
                decimal balanceBefore = SelectedCustomer.CurrentBalance;
                var customerSnapshot = SelectedCustomer;

                var payment = new CustomerPayment
                {
                    CustomerId    = SelectedCustomer.Id,
                    AmountPaid    = PaymentAmount,
                    PaymentDate   = DateTime.Now,
                    Note          = PaymentNote,
                    InvoiceNumber = PaymentInvoiceNumber?.Trim()
                };

                await _paymentRepository.AddAsync(payment);

                _lastPayment = payment;
                _lastPaymentCustomer = customerSnapshot;
                _balanceBeforeLastPayment = balanceBefore;

                PaymentAmount = 0;
                PaymentNote = null;
                PaymentInvoiceNumber = null;

                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == payment.CustomerId);

                NotificationHelper.ShowSuccess($"Payment of Rs.{payment.AmountPaid:N2} recorded!");

                PrintPaymentReceipt(payment, customerSnapshot, balanceBefore);
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("record payment", ex.Message);
            }
        }

        private void ReprintLastReceipt()
        {
            if (_lastPayment == null || _lastPaymentCustomer == null) return;
            PrintPaymentReceipt(_lastPayment, _lastPaymentCustomer, _balanceBeforeLastPayment);
        }

        private static void PrintPaymentReceipt(CustomerPayment payment, Customer customer, decimal balanceBefore)
        {
            try
            {
                var doc = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left
                };

                var header = new Paragraph { Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center };
                header.Inlines.Add(new Bold(new Run("Shahjee super store")) { FontSize = 22 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 13 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("0332-3324911") { FontSize = 13 });
                doc.Blocks.Add(header);

                doc.Blocks.Add(new Paragraph(new Bold(new Run("Payment Receipt / رسید")))
                {
                    TextAlignment = TextAlignment.Center,
                    FontSize = 15,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(3),
                    Margin = new Thickness(0, 2, 0, 4)
                });

                doc.Blocks.Add(Ln($"Date:      {payment.PaymentDate:dd-MMM-yyyy  hh:mm tt}"));
                doc.Blocks.Add(Ln($"Customer:  {customer.Name}"));
                if (!string.IsNullOrWhiteSpace(payment.InvoiceNumber))
                    doc.Blocks.Add(Ln($"Invoice #: {payment.InvoiceNumber}"));
                if (!string.IsNullOrWhiteSpace(customer.Phone))
                    doc.Blocks.Add(Ln($"Phone:     {customer.Phone}"));
                if (!string.IsNullOrWhiteSpace(payment.Note))
                    doc.Blocks.Add(Ln($"Note:      {payment.Note}"));

                doc.Blocks.Add(Separator());

                decimal remaining = balanceBefore - payment.AmountPaid;
                var table = new Table { CellSpacing = 0 };
                table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
                var tg = new TableRowGroup();
                table.RowGroups.Add(tg);

                AddTableRow(tg, "Previous Balance:", $"Rs. {balanceBefore:N2}", bold: false);
                AddTableRow(tg, "Amount Paid:", $"Rs. {payment.AmountPaid:N2}", bold: false);
                AddTableRow(tg, "Remaining Balance:", $"Rs. {remaining:N2}", bold: true);
                doc.Blocks.Add(table);

                doc.Blocks.Add(Separator());

                doc.Blocks.Add(new Paragraph(new Bold(new Run("Thank you for your payment!")))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 2)
                });

                var printDialog = new PrintDialog();
                bool small = SettingsManager.LoadSettings().UseSmallBillFormat;
                if (small)
                {
                    doc.PageWidth = 280;
                    doc.PageHeight = double.NaN;
                    doc.ColumnWidth = 260;
                    doc.PagePadding = new Thickness(5);
                    doc.FontSize = 10;
                }
                else
                {
                    doc.PageWidth = printDialog.PrintableAreaWidth;
                    doc.PageHeight = printDialog.PrintableAreaHeight;
                    doc.ColumnWidth = printDialog.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(40);
                }
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Payment Receipt");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print payment receipt", ex.Message);
            }
        }

        private static Paragraph Ln(string text) =>
            new Paragraph(new Run(text)) { Margin = new Thickness(0, 1, 0, 1) };

        private static Paragraph Separator() =>
            new Paragraph(new Run("─────────────────────────────────")) { Margin = new Thickness(0, 3, 0, 3) };

        private static void AddTableRow(TableRowGroup group, string label, string value, bool bold)
        {
            var row = new TableRow();
            var lp = new Paragraph(new Run(label)) { Margin = new Thickness(0) };
            var vp = new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
            if (bold) { lp.FontWeight = FontWeights.Bold; vp.FontWeight = FontWeights.Bold; }
            row.Cells.Add(new TableCell(lp) { Padding = new Thickness(2, 1, 2, 1) });
            row.Cells.Add(new TableCell(vp) { Padding = new Thickness(2, 1, 2, 1) });
            group.Rows.Add(row);
        }
    }
}
