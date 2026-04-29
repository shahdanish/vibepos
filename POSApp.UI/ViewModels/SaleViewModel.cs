using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
        private decimal _lastScannedCost;
        private bool _isLastScannedCostVisible;
        private readonly DispatcherTimer _costHideTimer;

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

        public decimal LastScannedCost
        {
            get => _lastScannedCost;
            set => SetProperty(ref _lastScannedCost, value);
        }

        public bool IsLastScannedCostVisible
        {
            get => _isLastScannedCostVisible;
            set => SetProperty(ref _isLastScannedCostVisible, value);
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

            _costHideTimer = new DispatcherTimer();
            _costHideTimer.Interval = TimeSpan.FromSeconds(3);
            _costHideTimer.Tick += (s, e) => { IsLastScannedCostVisible = false; _costHideTimer.Stop(); };

            SaleItems.CollectionChanged += SaleItems_CollectionChanged;

            AddItemCommand = new RelayCommand(_ => AddItem());
            DeleteItemCommand = new RelayCommand(DeleteItem);
            SaveCommand = new RelayCommand(async _ => await SaveSale());
            NewCommand = new RelayCommand(_ => NewSale());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintCommand = new RelayCommand(async _ => await PrintInvoice());

            _ = LoadData();
        }

        private void SaleItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SaleItemViewModel item in e.OldItems)
                    item.PropertyChanged -= SaleItem_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (SaleItemViewModel item in e.NewItems)
                    item.PropertyChanged += SaleItem_PropertyChanged;
            }
            CalculateTotals();
        }

        private void SaleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaleItemViewModel.Total) ||
                e.PropertyName == nameof(SaleItemViewModel.UnitPrice) ||
                e.PropertyName == nameof(SaleItemViewModel.Quantity) ||
                e.PropertyName == nameof(SaleItemViewModel.DiscountPercent))
            {
                CalculateTotals();
            }
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
                        Quantity = 1;
                        DiscountPercent = 0;

                        // Set temporary cost display
                        LastScannedCost = product.CostPrice;
                        IsLastScannedCostVisible = true;
                        _costHideTimer.Stop();
                        _costHideTimer.Start();

                        AddItem();
                    }

                    // Clear search text for next scan
                    ProductSearchText = string.Empty;
                }
            }
            catch (Exception)
            {
                // Silently ignore errors during auto-scan to prevent UI disruption
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
            // Skip validation if called from barcode scan (product is already selected)
            if (SelectedProduct == null)
            {
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

            SaleItems.Add(saleItem);

            CalculateTotals();

            // Set temporary cost display
            LastScannedCost = SelectedProduct.CostPrice;
            IsLastScannedCostVisible = true;
            _costHideTimer.Stop();
            _costHideTimer.Start();

            // Force UI refresh
            OnPropertyChanged(nameof(SaleItems));

            // Reset input fields
            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
            DiscountPercent = 0;
            ProductSearchText = string.Empty; // Auto-clear field
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
                // 1. Build the professional FlowDocument
                FlowDocument printDoc = CreateProfessionalInvoice();

                // 2. Initialize print dialog (no dialog shown)
                PrintDialog printDialog = new PrintDialog();

                // 3. Configure page size
                if (UseSmallBillFormat)
                {
                    printDoc.PageWidth = 280; // Safer for 80mm and better for scaling
                    printDoc.PageHeight = double.NaN;
                    printDoc.ColumnWidth = 260;
                    printDoc.PagePadding = new Thickness(5);
                    printDoc.FontSize = 10; // Smaller font for thermal
                }
                else
                {
                    printDoc.PageWidth = printDialog.PrintableAreaWidth;
                    printDoc.PageHeight = printDialog.PrintableAreaHeight;
                    printDoc.ColumnWidth = printDialog.PrintableAreaWidth;
                    printDoc.PagePadding = new Thickness(40);
                }

                // 4. Print the document immediately without dialog
                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                    "Invoice Printing");

                NotificationHelper.ShowSuccess($"Invoice {InvoiceNumber} printed successfully!");

                // 5. Save the sale after successful print
                await SaveSale();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print invoice", ex.Message);
            }
        }

        protected virtual string InvoiceTitle => "Bill / Invoice";

        private FlowDocument CreateProfessionalInvoice()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.TextAlignment = TextAlignment.Left;

            // --- HEADER ---
            Paragraph header = new Paragraph();
            header.TextAlignment = TextAlignment.Center;
            header.Inlines.Add(new Bold(new Run("Shah Jee Super Store")) { FontSize = 24 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 14 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("0332-3324911") { FontSize = 14 });
            doc.Blocks.Add(header);

            // --- BILL / INVOICE TITLE ---
            Paragraph titlePara = new Paragraph(new Bold(new Run(InvoiceTitle))) { TextAlignment = TextAlignment.Center, FontSize = 16 };
            Border titleBorder = new Border() { BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Padding = new Thickness(5) };
            // Note: FlowDocument doesn't support Border directly as a block, we use a Table with one cell or just paragraphs with borders
            titlePara.BorderBrush = Brushes.Black;
            titlePara.BorderThickness = new Thickness(1);
            titlePara.Padding = new Thickness(5);
            doc.Blocks.Add(titlePara);

            // --- METADATA TABLE (Bill No, Date, etc.) ---
            Table metaTable = new Table() { CellSpacing = 0 };
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });

            TableRowGroup metaGroup = new TableRowGroup();

            // Row 1: Bill No & Date
            TableRow row1 = new TableRow();
            row1.Cells.Add(new TableCell(new Paragraph(new Run($"Bill No: {InvoiceNumber}"))));
            row1.Cells.Add(new TableCell(new Paragraph(new Run($"Date: {SaleDate:dd-MMM-yyyy}"))));
            metaGroup.Rows.Add(row1);

            // Row 2: Customer
            TableRow row2 = new TableRow();
            row2.Cells.Add(new TableCell(new Paragraph(new Run($"Customer: {CustomerName}"))) { ColumnSpan = 2 });
            metaGroup.Rows.Add(row2);

            // Row 3: Address & Mobile
            TableRow row3 = new TableRow();
            row3.Cells.Add(new TableCell(new Paragraph(new Run($"Address: {Address ?? ""}"))));
            row3.Cells.Add(new TableCell(new Paragraph(new Run($"Mobile: {MobileNumber ?? ""}"))));
            metaGroup.Rows.Add(row3);

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            doc.Blocks.Add(new Paragraph(new Run("-----------------------------------------------------------------")));

            // --- ITEMS TABLE ---
            Table itemsTable = new Table() { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            if (UseSmallBillFormat)
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.4, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.1, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.6, GridUnitType.Star) }); // Qty
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.9, GridUnitType.Star) }); // Price
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.7, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1.1, GridUnitType.Star) }); // Total
            }
            else
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.5, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.5, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Qty
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Price
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Total
            }

            TableRowGroup itemsGroup = new TableRowGroup();

            // Header Row
            TableRow headerRow = new TableRow() { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("S.No"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Product Name"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Price"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Disc"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            itemsGroup.Rows.Add(headerRow);

            // Item Rows
            int serialNo = 1;
            foreach (var item in SaleItems)
            {
                TableRow row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(serialNo++.ToString()))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.ProductName))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Quantity.ToString()))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.UnitPrice.ToString("N0")))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.DiscountPercent.ToString("N0")))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Total.ToString("N2")))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                itemsGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // --- TOTALS SECTION ---
            Table totalsTable = new Table() { CellSpacing = 0 };
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Empty space
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(200) }); // Labels
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(100) }); // Values

            TableRowGroup totalsGroup = new TableRowGroup();

            // Total Bill
            TableRow tRow1 = new TableRow();
            tRow1.Cells.Add(new TableCell(new Paragraph(new Run("")))); // Empty
            tRow1.Cells.Add(new TableCell(new Paragraph(new Run("Total Bill")) { FontWeight = FontWeights.Bold }));
            tRow1.Cells.Add(new TableCell(new Paragraph(new Run(TotalBill.ToString("N2"))) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold }));
            totalsGroup.Rows.Add(tRow1);

            // Previous Balance
            if (PreBalance > 0)
            {
                TableRow tRowPB = new TableRow();
                tRowPB.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                tRowPB.Cells.Add(new TableCell(new Paragraph(new Run("Previous Balance"))));
                tRowPB.Cells.Add(new TableCell(new Paragraph(new Run(PreBalance.ToString("N2"))) { TextAlignment = TextAlignment.Right }));
                totalsGroup.Rows.Add(tRowPB);
            }

            // Net Total Bill
            TableRow tRowNet = new TableRow();
            tRowNet.Cells.Add(new TableCell(new Paragraph(new Run(""))));
            tRowNet.Cells.Add(new TableCell(new Paragraph(new Run("Net Bill Amount")) { FontWeight = FontWeights.Bold }));
            tRowNet.Cells.Add(new TableCell(new Paragraph(new Run((TotalBill + PreBalance).ToString("N2"))) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold }));
            totalsGroup.Rows.Add(tRowNet);

            // Cash Received
            TableRow tRow2 = new TableRow();
            tRow2.Cells.Add(new TableCell(new Paragraph(new Run("")))); // Empty
            tRow2.Cells.Add(new TableCell(new Paragraph(new Run("Cash Received"))));
            tRow2.Cells.Add(new TableCell(new Paragraph(new Run(ReceiveCash.ToString("N2"))) { TextAlignment = TextAlignment.Right }));
            totalsGroup.Rows.Add(tRow2);

            // Total Quantity
            TableRow tRow3 = new TableRow();
            tRow3.Cells.Add(new TableCell(new Paragraph(new Run("")))); // Empty
            tRow3.Cells.Add(new TableCell(new Paragraph(new Run("Total Items Quantity")) { FontWeight = FontWeights.Bold }));
            tRow3.Cells.Add(new TableCell(new Paragraph(new Run(SaleItems.Sum(i => i.Quantity).ToString())) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold }));
            totalsGroup.Rows.Add(tRow3);

            // Discount
            if (DiscountOnBill > 0 || DiscountOnProducts > 0)
            {
                TableRow tRow4 = new TableRow();
                tRow4.Cells.Add(new TableCell(new Paragraph(new Run("")))); // Empty
                tRow4.Cells.Add(new TableCell(new Paragraph(new Run("Total Discount"))));
                tRow4.Cells.Add(new TableCell(new Paragraph(new Run((DiscountOnBill + DiscountOnProducts).ToString("N2"))) { TextAlignment = TextAlignment.Right }));
                totalsGroup.Rows.Add(tRow4);
            }

            // Balance
            TableRow tRow5 = new TableRow();
            tRow5.Cells.Add(new TableCell(new Paragraph(new Run("")))); // Empty
            tRow5.Cells.Add(new TableCell(new Paragraph(new Run("Balance Amount")) { FontWeight = FontWeights.Bold, FontSize = 14 }));
            tRow5.Cells.Add(new TableCell(new Paragraph(new Run(Balance.ToString("N2"))) { TextAlignment = TextAlignment.Right, FontWeight = FontWeights.Bold, FontSize = 14 }));
            totalsGroup.Rows.Add(tRow5);

            totalsTable.RowGroups.Add(totalsGroup);
            doc.Blocks.Add(totalsTable);

            // --- FOOTER ---
            Paragraph footer = new Paragraph();
            footer.Margin = new Thickness(0, 20, 0, 0);
            footer.TextAlignment = TextAlignment.Center;
            footer.Inlines.Add(new Bold(new Run("Thank You For Your Business!")) { FontSize = 14 });
            footer.Inlines.Add(new LineBreak());
            footer.Inlines.Add(new Run("Please keep this invoice for your records.") { FontSize = 10, Foreground = Brushes.Gray });
            doc.Blocks.Add(footer);

            return doc;
        }
    }

    public sealed class SaleItemViewModel : ViewModelBase
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
