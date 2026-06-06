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
    public sealed class CustomerLedgerViewModel : ViewModelBase
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerPaymentRepository _paymentRepository;

        private Customer? _selectedCustomer;
        private decimal _paymentAmount;
        private string? _paymentNote;

        // Add customer fields
        private string _newCustomerName = string.Empty;
        private string? _newCustomerPhone;
        private string? _newCustomerAddress;
        private decimal _newCustomerInitialBalance;

        // Last payment tracking for receipt reprinting
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
                    _ = LoadPaymentsSafe();
            }
        }

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

        public ICommand AddPaymentCommand { get; }
        public ICommand AddCustomerCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PrintReceiptCommand { get; }

        public CustomerLedgerViewModel(ICustomerRepository customerRepository, ICustomerPaymentRepository paymentRepository)
        {
            _customerRepository = customerRepository;
            _paymentRepository = paymentRepository;

            AddPaymentCommand = new RelayCommand(async _ => await AddPayment());
            AddCustomerCommand = new RelayCommand(async _ => await AddCustomer());
            RefreshCommand = new RelayCommand(async _ => await LoadCustomers());
            PrintReceiptCommand = new RelayCommand(_ => ReprintLastReceipt(), _ => _lastPayment != null);

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
            Customers.Clear();
            foreach (var customer in customers.OrderByDescending(c => c.CurrentBalance))
                Customers.Add(customer);
        }

        private async Task LoadPaymentsSafe()
        {
            try { await LoadPayments(); }
            catch (Exception ex) { NotificationHelper.OperationFailed("load payment history", ex.Message); }
        }

        private async Task LoadPayments()
        {
            Payments.Clear();
            if (SelectedCustomer == null) return;
            var payments = await _paymentRepository.GetByCustomerIdAsync(SelectedCustomer.Id);
            foreach (var payment in payments)
                Payments.Add(payment);
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
                // Capture balance before the payment is applied
                decimal balanceBefore = SelectedCustomer.CurrentBalance;
                var customerSnapshot = SelectedCustomer;

                var payment = new CustomerPayment
                {
                    CustomerId = SelectedCustomer.Id,
                    AmountPaid = PaymentAmount,
                    PaymentDate = DateTime.Now,
                    Note = PaymentNote
                };

                await _paymentRepository.AddAsync(payment);

                // Store for reprinting
                _lastPayment = payment;
                _lastPaymentCustomer = customerSnapshot;
                _balanceBeforeLastPayment = balanceBefore;

                PaymentAmount = 0;
                PaymentNote = null;

                await LoadCustomers();
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == payment.CustomerId);

                NotificationHelper.ShowSuccess($"Payment of Rs.{payment.AmountPaid:N2} recorded!");

                // Auto-print receipt
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

                // Store header
                var header = new Paragraph { Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center };
                header.Inlines.Add(new Bold(new Run("Shahjee super store")) { FontSize = 22 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 13 });
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("0332-3324911") { FontSize = 13 });
                doc.Blocks.Add(header);

                // Title
                doc.Blocks.Add(new Paragraph(new Bold(new Run("Payment Receipt / رسید")))
                {
                    TextAlignment = TextAlignment.Center,
                    FontSize = 15,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(3),
                    Margin = new Thickness(0, 2, 0, 4)
                });

                // Date and customer info
                doc.Blocks.Add(Ln($"Date:      {payment.PaymentDate:dd-MMM-yyyy  hh:mm tt}"));
                doc.Blocks.Add(Ln($"Customer:  {customer.Name}"));
                if (!string.IsNullOrWhiteSpace(customer.Phone))
                    doc.Blocks.Add(Ln($"Phone:     {customer.Phone}"));
                if (!string.IsNullOrWhiteSpace(payment.Note))
                    doc.Blocks.Add(Ln($"Note:      {payment.Note}"));

                doc.Blocks.Add(Separator());

                // Balance breakdown table
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

                // Footer
                doc.Blocks.Add(new Paragraph(new Bold(new Run("Thank you for your payment!")))
                {
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 2)
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
