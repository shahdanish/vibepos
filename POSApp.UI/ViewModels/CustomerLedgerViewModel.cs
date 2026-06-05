using System.Collections.ObjectModel;
using System.Windows.Input;
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

        public ObservableCollection<Customer> Customers { get; } = new();
        public ObservableCollection<CustomerPayment> Payments { get; } = new();

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    // Load payments asynchronously with proper error handling
                    _ = LoadPaymentsSafe();
                }
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

        public CustomerLedgerViewModel(ICustomerRepository customerRepository, ICustomerPaymentRepository paymentRepository)
        {
            _customerRepository = customerRepository;
            _paymentRepository = paymentRepository;

            AddPaymentCommand = new RelayCommand(async _ => await AddPayment());
            AddCustomerCommand = new RelayCommand(async _ => await AddCustomer());
            RefreshCommand = new RelayCommand(async _ => await LoadCustomers());

            // Load customers on initialization with proper error handling
            _ = LoadCustomersSafe();
        }

        private async Task LoadCustomersSafe()
        {
            try
            {
                await LoadCustomers();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("load customers", ex.Message);
            }
        }

        private async Task LoadCustomers()
        {
            var customers = await _customerRepository.GetAllAsync();
            Customers.Clear();
            
            // Show customers with balances first
            foreach (var customer in customers.OrderByDescending(c => c.CurrentBalance))
            {
                Customers.Add(customer);
            }
        }

        private async Task LoadPaymentsSafe()
        {
            try
            {
                await LoadPayments();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("load payment history", ex.Message);
            }
        }

        private async Task LoadPayments()
        {
            Payments.Clear();
            
            if (SelectedCustomer == null) return;

            var payments = await _paymentRepository.GetByCustomerIdAsync(SelectedCustomer.Id);
            foreach (var payment in payments)
            {
                Payments.Add(payment);
            }
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
                var payment = new CustomerPayment
                {
                    CustomerId = SelectedCustomer.Id,
                    AmountPaid = PaymentAmount,
                    PaymentDate = DateTime.Now,
                    Note = PaymentNote
                };

                await _paymentRepository.AddAsync(payment);

                // Clear form
                PaymentAmount = 0;
                PaymentNote = null;

                // Reload customer list to get updated balances
                await LoadCustomers();

                // Re-select the same customer to refresh their payment history
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == payment.CustomerId);

                NotificationHelper.ShowSuccess($"Payment of Rs.{payment.AmountPaid:N2} recorded successfully!");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("record payment", ex.Message);
            }
        }
    }
}
