using System.Collections.ObjectModel;
using System.Windows;
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
                    _ = LoadPayments();
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

            _ = LoadCustomers();
        }

        private async Task LoadCustomers()
        {
            try
            {
                var customers = await _customerRepository.GetAllAsync();
                Customers.Clear();
                // Show customers with balances first
                foreach (var customer in customers.OrderByDescending(c => c.CurrentBalance))
                {
                    Customers.Add(customer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading customers: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadPayments()
        {
            Payments.Clear();
            if (SelectedCustomer == null) return;

            try
            {
                var payments = await _paymentRepository.GetByCustomerIdAsync(SelectedCustomer.Id);
                foreach (var payment in payments)
                {
                    Payments.Add(payment);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading payments: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddPayment()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select a customer.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentAmount <= 0)
            {
                MessageBox.Show("Please enter a valid payment amount.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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

                PaymentAmount = 0;
                PaymentNote = null;

                // Reload customer to get updated balance
                await LoadCustomers();

                // Re-select the same customer
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == payment.CustomerId);

                MessageBox.Show($"Payment of Rs.{payment.AmountPaid:N2} recorded!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording payment: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
