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

        public ICommand AddPaymentCommand { get; }
        public ICommand RefreshCommand { get; }

        public CustomerLedgerViewModel(ICustomerRepository customerRepository, ICustomerPaymentRepository paymentRepository)
        {
            _customerRepository = customerRepository;
            _paymentRepository = paymentRepository;

            AddPaymentCommand = new RelayCommand(async _ => await AddPayment());
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
