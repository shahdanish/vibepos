using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public class SaleReturnViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;

        private string _searchInvoiceNumber = string.Empty;
        private Sale? _originalSale;
        private string _returnInvoiceNumber = string.Empty;
        private DateTime _returnDate = DateTime.Now;
        private string _returnReason = string.Empty;
        private decimal _totalReturnAmount;

        public ObservableCollection<ReturnItemViewModel> ReturnItems { get; } = new();
        public ObservableCollection<Sale> SearchResults { get; } = new();

        public string SearchInvoiceNumber
        {
            get => _searchInvoiceNumber;
            set => SetProperty(ref _searchInvoiceNumber, value);
        }

        public Sale? OriginalSale
        {
            get => _originalSale;
            set
            {
                if (SetProperty(ref _originalSale, value) && value != null)
                {
                    LoadReturnItems(value);
                }
            }
        }

        public string ReturnInvoiceNumber
        {
            get => _returnInvoiceNumber;
            set => SetProperty(ref _returnInvoiceNumber, value);
        }

        public DateTime ReturnDate
        {
            get => _returnDate;
            set => SetProperty(ref _returnDate, value);
        }

        public string ReturnReason
        {
            get => _returnReason;
            set => SetProperty(ref _returnReason, value);
        }

        public decimal TotalReturnAmount
        {
            get => _totalReturnAmount;
            set => SetProperty(ref _totalReturnAmount, value);
        }

        public ICommand SearchCommand { get; }
        public ICommand ProcessReturnCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintReturnCommand { get; }

        public SaleReturnViewModel(ISaleRepository saleRepository, IProductRepository productRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;

            SearchCommand = new RelayCommand(async _ => await SearchInvoice());
            ProcessReturnCommand = new RelayCommand(async _ => await ProcessReturn());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintReturnCommand = new RelayCommand(_ => PrintReturnReceipt());

            _ = GenerateReturnInvoiceNumber();
        }

        private async Task GenerateReturnInvoiceNumber()
        {
            var nextNumber = await _saleRepository.GetNextInvoiceNumberAsync();
            ReturnInvoiceNumber = "R-" + nextNumber;
        }

        private async Task SearchInvoice()
        {
            if (string.IsNullOrWhiteSpace(SearchInvoiceNumber))
            {
                NotificationHelper.ValidationErrorCustom("Please enter an invoice number to search.");
                return;
            }

            try
            {
                var sale = await _saleRepository.GetByInvoiceNumberAsync(SearchInvoiceNumber);
                
                if (sale == null)
                {
                    NotificationHelper.ShowWarning($"Invoice '{SearchInvoiceNumber}' was not found in the system.", "Invoice Not Found");
                    return;
                }

                if (sale.SaleType == "Return")
                {
                    NotificationHelper.ShowWarning("This invoice is already a return transaction. You cannot process a return on a return.", "Invalid Operation");
                    return;
                }

                OriginalSale = sale;
                NotificationHelper.ShowInfo($"Invoice found successfully!\n\nCustomer: {sale.CustomerName}\nOriginal Amount: Rs.{sale.TotalBill:N2}", "Invoice Found");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("search invoice", ex.Message);
            }
        }

        private void LoadReturnItems(Sale sale)
        {
            ReturnItems.Clear();
            
            foreach (var item in sale.SaleItems)
            {
                ReturnItems.Add(new ReturnItemViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    OriginalQuantity = item.Quantity,
                    ReturnQuantity = 0, // User will specify
                    UnitPrice = item.UnitPrice,
                    Total = 0
                });
            }
        }

        private async Task ProcessReturn()
        {
            if (OriginalSale == null)
            {
                NotificationHelper.ValidationErrorCustom("Please search and select an invoice before processing the return.");
                return;
            }

            var itemsToReturn = ReturnItems.Where(i => i.ReturnQuantity > 0).ToList();
            
            if (!itemsToReturn.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please specify quantities to return for at least one item.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ReturnReason))
            {
                NotificationHelper.ValidationErrorCustom("Please provide a reason for this return.");
                return;
            }

            // Validate quantities
            foreach (var item in itemsToReturn)
            {
                if (item.ReturnQuantity > item.OriginalQuantity)
                {
                    NotificationHelper.ValidationErrorCustom($"Return quantity for '{item.ProductName}' cannot exceed the original quantity of {item.OriginalQuantity} units.");
                    return;
                }
            }

            try
            {
                // Create return sale
                var returnSale = new Sale
                {
                    InvoiceNumber = ReturnInvoiceNumber,
                    SaleDate = ReturnDate,
                    SaleType = "Return",
                    PaymentType = OriginalSale.PaymentType,
                    CustomerName = OriginalSale.CustomerName,
                    MobileNumber = OriginalSale.MobileNumber,
                    Address = OriginalSale.Address,
                    Phone = OriginalSale.Phone,
                    BillNote = $"Return for Invoice: {OriginalSale.InvoiceNumber}. Reason: {ReturnReason}",
                    TotalBill = -TotalReturnAmount, // Negative for return
                    ReceiveCash = -TotalReturnAmount,
                    Balance = 0
                };

                foreach (var item in itemsToReturn)
                {
                    returnSale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = -item.ReturnQuantity, // Negative for return
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        Total = -item.Total // Negative for return
                    });

                    // Update stock - add back returned items
                    var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.ReturnQuantity;
                        await _productRepository.UpdateAsync(product);
                    }
                }

                await _saleRepository.AddAsync(returnSale);
                
                NotificationHelper.ReturnProcessed(ReturnInvoiceNumber, TotalReturnAmount);

                // Reset form
                await GenerateReturnInvoiceNumber();
                OriginalSale = null;
                ReturnItems.Clear();
                SearchInvoiceNumber = string.Empty;
                ReturnReason = string.Empty;
                TotalReturnAmount = 0;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("process return", ex.Message);
            }
        }

        private void Cancel()
        {
            // Close window logic in code-behind
        }

        private void PrintReturnReceipt()
        {
            if (OriginalSale == null)
            {
                NotificationHelper.ValidationErrorCustom("Please search and select an invoice before printing the receipt.");
                return;
            }

            var receipt = GenerateReturnReceipt();
            NotificationHelper.ShowInfo($"Return Receipt\n\n{receipt}\n\nNote: Connect to printer for actual printing.", "Print Preview");
        }

        private string GenerateReturnReceipt()
        {
            var receipt = $"SHAH JEE SUPER STORE\\n";
            receipt += $"===== RETURN RECEIPT =====\\n";
            receipt += $"Return Invoice: {ReturnInvoiceNumber}\\n";
            receipt += $"Return Date: {ReturnDate:dd/MM/yyyy HH:mm}\\n";
            receipt += $"Original Invoice: {OriginalSale?.InvoiceNumber}\\n";
            receipt += $"Customer: {OriginalSale?.CustomerName}\\n";
            receipt += $"Reason: {ReturnReason}\\n";
            receipt += $"================================\\n\\n";
            receipt += $"Returned Items:\\n";

            foreach (var item in ReturnItems.Where(i => i.ReturnQuantity > 0))
            {
                receipt += $"{item.ProductName}\\n";
                receipt += $"  Qty: {item.ReturnQuantity} x Rs.{item.UnitPrice:N2} = Rs.{item.Total:N2}\\n";
            }

            receipt += $"\\n================================\\n";
            receipt += $"TOTAL REFUND: Rs.{TotalReturnAmount:N2}\\n";
            receipt += $"================================\\n";
            receipt += $"Thank you!\\n";

            return receipt;
        }
    }

    public class ReturnItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private int _originalQuantity;
        private int _returnQuantity;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _total;

        public string ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public int OriginalQuantity
        {
            get => _originalQuantity;
            set => SetProperty(ref _originalQuantity, value);
        }

        public int ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                if (SetProperty(ref _returnQuantity, value))
                {
                    Total = value * UnitPrice;
                }
            }
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set => SetProperty(ref _costPrice, value);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }
    }
}
