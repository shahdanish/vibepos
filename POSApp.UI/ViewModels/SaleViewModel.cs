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
    public class SaleViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;

        private string _invoiceNumber = string.Empty;
        private DateTime _saleDate = DateTime.Now;
        private string _paymentType = "Cash";
        private string _customerName = "Cash";
        private string? _address;
        private string? _phone;
        private string? _mobileNumber;
        private decimal _preBalance;
        private string? _billNote;
        private decimal _discountOnProducts;
        private decimal _discountOnBill;
        private decimal _totalBill;
        private decimal _receiveCash;
        private decimal _balance;
        private string _productSearchText = string.Empty;
        private Product? _selectedProduct;
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private bool _showPurchasePrice = true;
        private bool _autoPrint = false;
        private bool _autoAddItem = true;
        private bool _useSmallBillFormat = false;

        public ObservableCollection<SaleItemViewModel> SaleItems { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Customer> Customers { get; } = new();

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        public DateTime SaleDate
        {
            get => _saleDate;
            set => SetProperty(ref _saleDate, value);
        }

        public string PaymentType
        {
            get => _paymentType;
            set => SetProperty(ref _paymentType, value);
        }

        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        public string? Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string? MobileNumber
        {
            get => _mobileNumber;
            set => SetProperty(ref _mobileNumber, value);
        }

        public decimal PreBalance
        {
            get => _preBalance;
            set => SetProperty(ref _preBalance, value);
        }

        public string? BillNote
        {
            get => _billNote;
            set => SetProperty(ref _billNote, value);
        }

        public decimal DiscountOnProducts
        {
            get => _discountOnProducts;
            set
            {
                if (SetProperty(ref _discountOnProducts, value))
                    CalculateTotals();
            }
        }

        public decimal DiscountOnBill
        {
            get => _discountOnBill;
            set
            {
                if (SetProperty(ref _discountOnBill, value))
                    CalculateTotals();
            }
        }

        public decimal TotalBill
        {
            get => _totalBill;
            set => SetProperty(ref _totalBill, value);
        }

        public decimal ReceiveCash
        {
            get => _receiveCash;
            set
            {
                if (SetProperty(ref _receiveCash, value))
                    CalculateBalance();
            }
        }

        public decimal Balance
        {
            get => _balance;
            set => SetProperty(ref _balance, value);
        }

        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                { 
                    // Auto-add on barcode scan if text is entered (from scanner)
                    if (AutoAddItem && !string.IsNullOrWhiteSpace(value) && value.Length >= 3)
                    {
                        _ = AutoAddProductFromBarcode(value);
                    }
                    else
                    {
                        _ = SearchProducts();
                    }
                }
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    UnitPrice = value.UnitPrice;
                    
                    // Auto-add item if enabled and barcode scanned
                    // Disable during initial load to prevent crashes
                    if (AutoAddItem && !string.IsNullOrWhiteSpace(ProductSearchText) && SaleItems != null)
                    {
                        try
                        {
                            AddItem();
                            ProductSearchText = string.Empty;
                        }
                        catch
                        {
                            // Ignore errors during auto-add
                        }
                    }
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set => SetProperty(ref _discountPercent, value);
        }

        public bool ShowPurchasePrice
        {
            get => _showPurchasePrice;
            set
            {
                if (SetProperty(ref _showPurchasePrice, value))
                {
                    SettingsManager.SaveSetting(s => s.ShowPurchasePrice = value);
                }
            }
        }

        public bool AutoPrint
        {
            get => _autoPrint;
            set
            {
                if (SetProperty(ref _autoPrint, value))
                {
                    SettingsManager.SaveSetting(s => s.AutoPrint = value);
                }
            }
        }

        public bool AutoAddItem
        {
            get => _autoAddItem;
            set
            {
                if (SetProperty(ref _autoAddItem, value))
                {
                    SettingsManager.SaveSetting(s => s.AutoAddItem = value);
                }
            }
        }

        public bool UseSmallBillFormat
        {
            get => _useSmallBillFormat;
            set
            {
                if (SetProperty(ref _useSmallBillFormat, value))
                {
                    SettingsManager.SaveSetting(s => s.UseSmallBillFormat = value);
                }
            }
        }

        public decimal TotalPurchasePrice => SaleItems?.Sum(item => item.CostPrice * item.Quantity) ?? 0;

        public ICommand AddItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintCommand { get; }

        public SaleViewModel(ISaleRepository saleRepository, IProductRepository productRepository, ICustomerRepository customerRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;

            // Load saved settings
            var settings = SettingsManager.LoadSettings();
            _autoPrint = settings.AutoPrint;
            _useSmallBillFormat = settings.UseSmallBillFormat;
            _autoAddItem = settings.AutoAddItem;
            _showPurchasePrice = settings.ShowPurchasePrice;

            AddItemCommand = new RelayCommand(_ => AddItem());
            DeleteItemCommand = new RelayCommand(DeleteItem);
            SaveCommand = new RelayCommand(async _ => await SaveSale());
            NewCommand = new RelayCommand(_ => NewSale());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintCommand = new RelayCommand(async _ => await PrintInvoice());

            _ = LoadData();
        }

        private async Task LoadData()
        {
            InvoiceNumber = await _saleRepository.GetNextInvoiceNumberAsync();
            
            var products = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            var customers = await _customerRepository.GetAllAsync();
            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }
        }

        private async Task AutoAddProductFromBarcode(string barcode)
        {
            try
            {
                // Try to find product by barcode
                var product = await _productRepository.GetByBarcodeAsync(barcode);
                
                if (product != null)
                {
                    // Check if product is deleted
                    if (product.IsDeleted)
                    {
                        NotificationHelper.ShowError($"Product '{product.ProductName}' has been deleted and cannot be sold.");
                        ProductSearchText = string.Empty; // Clear field
                        return;
                    }
                    
                    // Check stock availability
                    if (product.Stock <= 0)
                    {
                        NotificationHelper.ShowError($"Product '{product.ProductName}' is out of stock!");
                        ProductSearchText = string.Empty;
                        return;
                    }
                    
                    // Check if item already in cart - increase quantity instead of duplicate
                    var existingItem = SaleItems.FirstOrDefault(item => item.ProductId == product.ProductId);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += 1;
                        CalculateTotals();
                        NotificationHelper.ShowSuccess($"Quantity increased for {product.ProductName}");
                    }
                    else
                    {
                        // Add new item to cart
                        SelectedProduct = product;
                        UnitPrice = product.UnitPrice;
                        Quantity = 1;
                        DiscountPercent = 0;
                        AddItem();
                    }
                    
                    // Clear search text for next scan
                    ProductSearchText = string.Empty;
                }
            }
            catch (Exception ex)
            {
                // Silently ignore errors during auto-scan to prevent UI disruption
                System.Diagnostics.Debug.WriteLine($"Auto-add error: {ex.Message}");
            }
        }

        private async Task SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                await LoadData();
                return;
            }

            // Search by Product ID or Barcode
            var productByBarcode = await _productRepository.GetByProductIdAsync(ProductSearchText);
            if (productByBarcode == null)
            {
                var products = await _productRepository.SearchAsync(ProductSearchText);
                Products.Clear();
                foreach (var product in products)
                {
                    Products.Add(product);
                }
            }
            else
            {
                // Direct barcode match - auto-select
                Products.Clear();
                Products.Add(productByBarcode);
                SelectedProduct = productByBarcode;
            }
        }

        private void AddItem()
        {
            System.Diagnostics.Debug.WriteLine($"=== AddItem called ===");
            System.Diagnostics.Debug.WriteLine($"SelectedProduct: {SelectedProduct?.ProductName ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"Quantity: {Quantity}");
            
            // Skip validation if called from barcode scan (product is already selected)
            if (SelectedProduct == null)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR: No product selected!");
                NotificationHelper.ValidationErrorCustom("Please select a product and enter a valid quantity to add to the sale.");
                return;
            }

            if (Quantity <= 0)
            {
                Quantity = 1; // Default to 1 if invalid
            }

            var total = (UnitPrice * Quantity) - ((UnitPrice * Quantity * DiscountPercent) / 100);
            
            var saleItem = new SaleItemViewModel
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName,
                Quantity = Quantity,
                CostPrice = SelectedProduct.CostPrice, // Track cost price (encrypted in display)
                UnitPrice = UnitPrice,
                DiscountPercent = DiscountPercent,
                Total = total
            };

            System.Diagnostics.Debug.WriteLine($"Adding item to SaleItems collection...");
            SaleItems.Add(saleItem);
            System.Diagnostics.Debug.WriteLine($"SaleItems count: {SaleItems.Count}");
            
            CalculateTotals();
            
            // Force UI refresh
            OnPropertyChanged(nameof(SaleItems));
            
            // Reset input fields
            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
            DiscountPercent = 0;
            
            System.Diagnostics.Debug.WriteLine($"=== AddItem completed ===");
        }

        private void DeleteItem(object? parameter)
        {
            if (parameter is SaleItemViewModel item)
            {
                SaleItems.Remove(item);
                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            if (SaleItems == null) return;
            
            var subtotal = SaleItems.Sum(item => item.Total);
            subtotal -= DiscountOnProducts;
            TotalBill = subtotal - DiscountOnBill;
            CalculateBalance();
            OnPropertyChanged(nameof(TotalPurchasePrice));
        }

        private void CalculateBalance()
        {
            Balance = ReceiveCash - TotalBill;
        }

        private async Task SaveSale()
        {
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please add at least one item before saving the sale.");
                return;
            }

            try
            {
                var sale = new Sale
                {
                    InvoiceNumber = InvoiceNumber,
                    SaleDate = SaleDate,
                    SaleType = "Sale",
                    PaymentType = PaymentType,
                    CustomerName = CustomerName,
                    Address = Address,
                    Phone = Phone,
                    MobileNumber = MobileNumber,
                    PreBalance = PreBalance,
                    BillNote = BillNote,
                    DiscountOnProducts = DiscountOnProducts,
                    DiscountOnBill = DiscountOnBill,
                    TotalBill = TotalBill,
                    ReceiveCash = ReceiveCash,
                    Balance = Balance,
                    AutoPrinted = AutoPrint
                };

                foreach (var item in SaleItems)
                {
                    sale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        CostPrice = item.CostPrice, // Save cost price for profit calculation
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        Total = item.Total
                    });
                }

                await _saleRepository.AddAsync(sale);
                
                // Auto-print if enabled
                if (AutoPrint)
                {
                    await PrintInvoice();
                }
                
                NewSale();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save sale", ex.Message);
            }
        }

        private void NewSale()
        {
            SaleItems.Clear();
            CustomerName = "Cash";
            Address = null;
            Phone = null;
            MobileNumber = null;
            PreBalance = 0;
            BillNote = null;
            DiscountOnProducts = 0;
            DiscountOnBill = 0;
            TotalBill = 0;
            ReceiveCash = 0;
            Balance = 0;
            OnPropertyChanged(nameof(TotalPurchasePrice));
            _ = LoadData();
        }

        private void Cancel()
        {
            // Close window logic will be handled in code-behind
        }

        private async Task PrintInvoice()
        {
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("No items to print. Please add items to the sale first.");
                return;
            }

            try
            {
                // 1. Generate the invoice text BEFORE saving (which clears items)
                var invoiceText = UseSmallBillFormat ? GenerateSmallBillText() : GenerateInvoiceText();

                // 2. Build a FlowDocument for printing
                FlowDocument printDoc = CreateInvoiceFlowDocument(invoiceText);

                // 3. Show standard Windows print dialog (pick printer, copies, etc.)
                PrintDialog printDialog = new PrintDialog();
                
                if (printDialog.ShowDialog() == true)
                {
                    // 4. Configure page size to match selected printer
                    if (UseSmallBillFormat)
                    {
                        printDoc.PageWidth = 302;
                        printDoc.PageHeight = 5000;
                        printDoc.ColumnWidth = 280;
                    }
                    else
                    {
                        printDoc.PageWidth = printDialog.PrintableAreaWidth;
                        printDoc.PageHeight = printDialog.PrintableAreaHeight;
                        printDoc.ColumnWidth = printDialog.PrintableAreaWidth;
                        printDoc.PagePadding = new Thickness(50);
                    }

                    // 5. Print the document
                    printDialog.PrintDocument(
                        ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                        "Invoice Printing");

                    NotificationHelper.ShowSuccess($"Invoice {InvoiceNumber} sent to printer!");

                    // 6. Save the sale after successful print
                    await SaveSale();
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print invoice", ex.Message);
            }
        }

        private FlowDocument CreateInvoiceFlowDocument(string invoiceText)
        {
            FlowDocument flowDoc = new FlowDocument();
            
            // Set formatting
            flowDoc.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            flowDoc.FontSize = 11;
            flowDoc.PagePadding = new Thickness(50);
            flowDoc.LineHeight = 16;
            
            // Parse invoice text and create paragraphs
            string[] lines = invoiceText.Split('\n');
            foreach (string line in lines)
            {
                Paragraph para = new Paragraph();
                para.Margin = new Thickness(0, 2, 0, 2);
                
                // Check if it's a header or separator line
                bool isHeader = line.Contains("SHAH JEE") || line.Contains("====") || 
                               line.StartsWith("Inv:") || line.StartsWith("Shah Jee") ||
                               line.StartsWith("Items:") || line.Contains("Total") || 
                               line.Contains("Cash") || line.Contains("Balance");
                
                if (isHeader)
                {
                    para.FontWeight = FontWeights.Bold;
                    para.FontSize = 12;
                }
                
                // Add text with proper formatting
                Run run = new Run(line);
                para.Inlines.Add(run);
                flowDoc.Blocks.Add(para);
            }
            
            return flowDoc;
        }

        private string GenerateInvoiceText()
        {
            var invoice = $"SHAH JEE SUPER STORE\n";
            invoice += $"================================\n";
            invoice += $"Invoice: {InvoiceNumber}\n";
            invoice += $"Date: {SaleDate:dd/MM/yyyy HH:mm}\n";
            invoice += $"Customer: {CustomerName}\n";
            if (!string.IsNullOrWhiteSpace(MobileNumber))
                invoice += $"Mobile: {MobileNumber}\n";
            invoice += $"Type: Sale\n";
            if (!string.IsNullOrWhiteSpace(BillNote))
                invoice += $"Note: {BillNote}\n";
            invoice += $"================================\n\n";
            invoice += $"Items:\n";
            
            foreach (var item in SaleItems)
            {
                invoice += $"{item.ProductName}\n";
                invoice += $"  {item.Quantity} x Rs.{item.UnitPrice:N2}";
                if (item.DiscountPercent > 0)
                    invoice += $" (-{item.DiscountPercent}%)";
                invoice += $" = Rs.{item.Total:N2}\n";
            }
            
            invoice += $"\n================================\n";
            if (DiscountOnProducts > 0)
                invoice += $"Discount on Products: Rs.{DiscountOnProducts:N2}\n";
            if (DiscountOnBill > 0)
                invoice += $"Discount on Bill: Rs.{DiscountOnBill:N2}\n";
            invoice += $"Total Bill: Rs.{TotalBill:N2}\n";
            invoice += $"Cash Received: Rs.{ReceiveCash:N2}\n";
            invoice += $"Balance: Rs.{Balance:N2}\n";
            invoice += $"================================\n";
            invoice += $"Thank you for your business!\n";
            
            return invoice;
        }

        // Compact bill format for thermal printers (58mm/80mm)
        private string GenerateSmallBillText()
        {
            var bill = "";
            
            // Header
            bill += "SHAH JEE SUPER STORE\n";
            bill += "-----------------------------\n";
            bill += $"Inv: {InvoiceNumber}\n";
            bill += $"Date: {SaleDate:dd/MM/yyyy HH:mm}\n";
            if (!string.IsNullOrWhiteSpace(CustomerName) && CustomerName != "Cash")
                bill += $"Cust: {CustomerName}\n";
            if (!string.IsNullOrWhiteSpace(MobileNumber))
                bill += $"Ph: {MobileNumber}\n";
            bill += "-----------------------------\n";
            
            // Items header
            bill += "Item         Qty  Price   Amount\n";
            bill += "---------------------------------\n";
            
            // Items
            foreach (var item in SaleItems)
            {
                var itemName = item.ProductName.Length > 12 ? item.ProductName.Substring(0, 10) + ".." : item.ProductName.PadRight(12);
                bill += $"{itemName} {item.Quantity,3}x{item.UnitPrice,6:N0}";
                if (item.DiscountPercent > 0)
                    bill += $"(-{item.DiscountPercent:N0}%)";
                else
                    bill += "       ";
                bill += $"{item.Total,7:N0}\n";
            }
            
            bill += "---------------------------------\n";
            
            // Totals
            if (DiscountOnProducts > 0)
                bill += $"Disc(Prod): Rs.{DiscountOnProducts,-6:N0}\n";
            if (DiscountOnBill > 0)
                bill += $"Disc(Bill): Rs.{DiscountOnBill,-6:N0}\n";
            bill += $"TOTAL:     Rs.{TotalBill,-6:N0}\n";
            bill += $"Cash:      Rs.{ReceiveCash,-6:N0}\n";
            bill += $"Balance:   Rs.{Balance,-6:N0}\n";
            bill += "---------------------------------\n";
            if (!string.IsNullOrWhiteSpace(BillNote))
                bill += $"{BillNote}\n";
            bill += "Thank you for your business!\n";
            
            return bill;
        }
    }

    public class SaleItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private int _quantity;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _discountPercent;
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

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    // Auto-calculate total when quantity changes
                    CalculateTotal();
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
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    // Auto-calculate total when unit price changes
                    CalculateTotal();
                }
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (SetProperty(ref _discountPercent, value))
                {
                    // Auto-calculate total when discount changes
                    CalculateTotal();
                }
            }
        }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }
        
        private void CalculateTotal()
        {
            Total = (UnitPrice * Quantity) - ((UnitPrice * Quantity * DiscountPercent) / 100);
        }
    }
}
