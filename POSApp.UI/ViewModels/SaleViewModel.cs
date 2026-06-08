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
        private Customer? _selectedCustomer;
        private string? _address;
        private string? _phone;
        private string? _mobileNumber;
        private decimal _preBalance;
        private string? _billNote;
        private decimal? _discountOnProducts;
        private decimal? _discountOnBill;
        private decimal _totalBill;
        private decimal? _receiveCash;
        private decimal _balance;
        private string _productSearchText = string.Empty;
        private string _barcodeInput = string.Empty;
        private Product? _selectedProduct;
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private bool _showPurchasePrice = true;
        private bool _autoPrint = false;
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

        public IReadOnlyList<string> PaymentTypes { get; } = new[] { "Cash", "Credit", "Credit Card", "Bank Transfer" };

        public string PaymentType
        {
            get => _paymentType;
            set
            {
                if (SetProperty(ref _paymentType, value))
                    OnPropertyChanged(nameof(IsCreditPayment));
            }
        }

        public bool IsCreditPayment => _paymentType == "Credit";

        public Customer? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value) && value != null)
                {
                    CustomerName = value.Name;
                    MobileNumber = value.CellNo ?? value.Phone;
                    PreBalance = value.CurrentBalance;
                }
            }
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

        public decimal? DiscountOnProducts
        {
            get => _discountOnProducts;
            set
            {
                if (SetProperty(ref _discountOnProducts, value))
                    CalculateTotals();
            }
        }

        public decimal? DiscountOnBill
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

        public decimal? ReceiveCash
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
            set => SetProperty(ref _productSearchText, value);
        }

        /// <summary>
        /// Dedicated barcode-scan field. Submitting it (Enter / scanner) runs ScanCommand,
        /// which looks the product up and adds it — independent of the product dropdown.
        /// </summary>
        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    UnitPrice = GetUnitPriceForProduct(value);

                    // Selecting from the dropdown auto-adds the item (qty 1) when Auto Add
                    // is enabled; otherwise the user adjusts qty/disc and clicks Add Item.
                    if (AutoAddItem && SaleItems != null)
                    {
                        AddItem();
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

        /// <summary>
        /// Product selection always auto-adds to the cart. The toggle is hidden in the UI
        /// and this is hard-wired to true so both Sale and Wholesale behave consistently.
        /// </summary>
        public bool AutoAddItem
        {
            get => true;
            set { }
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
            set
            {
                if (SetProperty(ref _lastScannedCost, value))
                    OnPropertyChanged(nameof(PrintButtonText));
            }
        }

        /// <summary>
        /// Print button caption — shows the last item's cost in brackets, e.g. "PRINT (60.00)".
        /// </summary>
        public string PrintButtonText =>
            LastScannedCost > 0 ? $"PRINT ({LastScannedCost:N2})" : "PRINT";

        public bool IsLastScannedCostVisible
        {
            get => _isLastScannedCostVisible;
            set => SetProperty(ref _isLastScannedCostVisible, value);
        }

        public decimal TotalPurchasePrice => SaleItems?.Sum(item => item.CostPrice * item.Quantity) ?? 0;
        public decimal TotalItemsDiscount => SaleItems?.Sum(item => item.EffectiveDiscountAmount) ?? 0;

        public Action? OpenQuickSaleWindow { get; set; }
        public Action? SwitchMode { get; set; }

        public virtual string ModeSwitchLabel => "⇄ WHOLESALE";

        public ICommand AddItemCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand QuickSaleCommand { get; }
        public ICommand SwitchModeCommand { get; }

        public SaleViewModel(ISaleRepository saleRepository, IProductRepository productRepository, ICustomerRepository customerRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;

            // Load saved settings
            var settings = SettingsManager.LoadSettings();
            _autoPrint = settings.AutoPrint;
            _useSmallBillFormat = settings.UseSmallBillFormat;
            _showPurchasePrice = settings.ShowPurchasePrice;

            _costHideTimer = new DispatcherTimer();
            _costHideTimer.Interval = TimeSpan.FromSeconds(3);
            _costHideTimer.Tick += (s, e) => { IsLastScannedCostVisible = false; _costHideTimer.Stop(); };

            SaleItems.CollectionChanged += SaleItems_CollectionChanged;

            AddItemCommand = new RelayCommand(_ => AddItem());
            ScanCommand = new RelayCommand(async _ => await ProcessBarcodeScan());
            DeleteItemCommand = new RelayCommand(DeleteItem);
            SaveCommand = new RelayCommand(async _ => await SaveSale(printAfterSave: false));
            NewCommand = new RelayCommand(_ => NewSale());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintCommand = new RelayCommand(async _ => await PrintInvoice());
            QuickSaleCommand = new RelayCommand(_ => OpenQuickSaleWindow?.Invoke());
            SwitchModeCommand = new RelayCommand(_ => SwitchMode?.Invoke());

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
                e.PropertyName == nameof(SaleItemViewModel.DiscountPercent) ||
                e.PropertyName == nameof(SaleItemViewModel.DiscountType))
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

        /// <summary>
        /// Resolves the selling price for a product. Overridden by WholeSaleViewModel
        /// so that barcode scanning uses the same wholesale pricing as a manual selection.
        /// </summary>
        protected virtual decimal GetUnitPriceForProduct(Product product) => product.UnitPrice;

        private async Task ProcessBarcodeScan()
        {
            var barcode = BarcodeInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            try
            {
                // Resolve by barcode first, then fall back to the Product ID so that
                // codes stored in either field auto-select exactly like a dropdown pick.
                var product = await _productRepository.GetByBarcodeAsync(barcode)
                              ?? await _productRepository.GetByProductIdAsync(barcode);

                if (product == null)
                {
                    NotificationHelper.ShowError($"No product found for code '{barcode}'.");
                    BarcodeInput = string.Empty;
                    return;
                }

                // Check if product is deleted
                if (product.IsDeleted)
                {
                    NotificationHelper.ShowError($"Product '{product.ProductName}' has been deleted and cannot be sold.");
                    BarcodeInput = string.Empty; // Clear field
                    return;
                }

                // Check stock availability
                if (product.Stock <= 0)
                {
                    NotificationHelper.ShowError($"Product '{product.ProductName}' is out of stock!");
                    BarcodeInput = string.Empty;
                    return;
                }

                // Check if item already in cart - increase quantity instead of duplicate
                var existingItem = SaleItems.FirstOrDefault(item => item.ProductId == product.ProductId);
                if (existingItem != null)
                {
                    existingItem.Quantity += 1;
                    CalculateTotals();
                }
                else
                {
                    // Build the line item directly (price respects Sale vs Whole Sale)
                    var price = GetUnitPriceForProduct(product);
                    var saleItem = new SaleItemViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Quantity = 1,
                        CostPrice = product.CostPrice,
                        UnitPrice = price,
                        DiscountPercent = 0,
                        Total = price
                    };
                    SaleItems.Add(saleItem);
                    CalculateTotals();

                    // Set temporary cost display
                    LastScannedCost = product.CostPrice;
                    IsLastScannedCostVisible = true;
                    _costHideTimer.Stop();
                    _costHideTimer.Start();
                }

                // Clear scan field, keep focus here for the next scan
                BarcodeInput = string.Empty;
            }
            catch (Exception)
            {
                // Silently ignore errors during auto-scan to prevent UI disruption
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

            // If the product is already in the cart, bump its quantity instead of
            // adding a duplicate row (matches the barcode-scan behaviour).
            var existingItem = SaleItems.FirstOrDefault(i => i.ProductId == SelectedProduct.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += Quantity;
            }
            else
            {
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
            }

            CalculateTotals();

            // Set temporary cost display
            LastScannedCost = SelectedProduct.CostPrice;
            IsLastScannedCostVisible = true;
            _costHideTimer.Stop();
            _costHideTimer.Start();

            // Reset entry fields synchronously
            Quantity = 1;
            UnitPrice = 0;
            DiscountPercent = 0;
            ProductSearchText = string.Empty;

            // Defer SelectedProduct reset so it runs after WPF finishes processing the
            // current ComboBox SelectionChanged event. Setting it synchronously here
            // (re-entrant setter call) prevents the ComboBox from properly clearing its
            // internal selection state, which stops subsequent product selections from firing.
            Application.Current.Dispatcher.BeginInvoke(() => { SelectedProduct = null; });
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
            subtotal -= DiscountOnProducts ?? 0;
            TotalBill = subtotal - (DiscountOnBill ?? 0);
            CalculateBalance();
            OnPropertyChanged(nameof(TotalPurchasePrice));
            OnPropertyChanged(nameof(TotalItemsDiscount));
        }

        private void CalculateBalance()
        {
            Balance = (ReceiveCash ?? 0) - TotalBill;
        }

        private async Task SaveSale(bool printAfterSave = false)
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
                    CustomerId = SelectedCustomer?.Id,
                    CustomerName = CustomerName,
                    Address = Address,
                    Phone = Phone,
                    MobileNumber = MobileNumber,
                    PreBalance = PreBalance,
                    BillNote = BillNote,
                    DiscountOnProducts = DiscountOnProducts ?? 0,
                    DiscountOnBill = DiscountOnBill ?? 0,
                    TotalBill = TotalBill,
                    ReceiveCash = ReceiveCash ?? 0,
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
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountType = item.DiscountType,
                        Total = item.Total
                    });
                }

                await _saleRepository.AddAsync(sale);

                // Decrement stock for each sold item
                foreach (var item in SaleItems)
                {
                    var product = Products.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - item.Quantity);
                        await _productRepository.UpdateAsync(product);
                    }
                }

                // Update customer balance for credit sales
                if (PaymentType == "Credit" && SelectedCustomer != null)
                {
                    SelectedCustomer.CurrentBalance += TotalBill;
                    SelectedCustomer.TotalPurchases += TotalBill;
                    SelectedCustomer.LastPurchaseDate = SaleDate;
                    SelectedCustomer.ModifiedDate = DateTime.Now;
                    await _customerRepository.UpdateAsync(SelectedCustomer);
                }

                // Auto-print if requested (prints only — does NOT re-enter SaveSale)
                if (printAfterSave)
                {
                    DoPrint();
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
            SelectedCustomer = null;
            PaymentType = "Cash";
            Address = null;
            Phone = null;
            MobileNumber = null;
            PreBalance = 0;
            BillNote = null;
            DiscountOnProducts = null;
            DiscountOnBill = null;
            TotalBill = 0;
            ReceiveCash = null;
            Balance = 0;
            BarcodeInput = string.Empty;
            LastScannedCost = 0;
            IsLastScannedCostVisible = false;
            OnPropertyChanged(nameof(TotalPurchasePrice));
            _ = LoadData();
        }

        private void Cancel()
        {
            // Close window logic will be handled in code-behind
        }

        public void LoadState(SaleViewModel source)
        {
            CustomerName = source.CustomerName;
            MobileNumber = source.MobileNumber;
            Address = source.Address;
            Phone = source.Phone;
            PaymentType = source.PaymentType;
            SelectedCustomer = source.SelectedCustomer;
            BillNote = source.BillNote;
            DiscountOnProducts = source.DiscountOnProducts;
            DiscountOnBill     = source.DiscountOnBill;
            ReceiveCash        = source.ReceiveCash;
            PreBalance         = source.PreBalance;

            SaleItems.Clear();
            foreach (var item in source.SaleItems)
            {
                SaleItems.Add(new SaleItemViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    CostPrice = item.CostPrice,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    DiscountType = item.DiscountType,
                });
            }
            CalculateTotals();
        }

        private async Task PrintInvoice()
        {
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("No items to print. Please add items to the sale first.");
                return;
            }

            // Print first, then save once. Pass printAfterSave: false so SaveSale
            // does not print again (which previously caused an endless popup loop).
            if (DoPrint())
            {
                await SaveSale(printAfterSave: false);
            }
        }

        /// <summary>
        /// Renders and prints the current invoice. Returns true on success.
        /// Pure print operation — does NOT save the sale, to avoid recursion.
        /// </summary>
        private bool DoPrint()
        {
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

                // No success popup for printing — just print silently.
                return true;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print invoice", ex.Message);
                return false;
            }
        }

        protected virtual string InvoiceTitle => "Bill / Invoice";

        protected virtual bool ShowCostPriceOnReceipt => false;

        private FlowDocument CreateProfessionalInvoice()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.TextAlignment = TextAlignment.Left;

            // --- HEADER ---
            Paragraph header = new Paragraph();
            header.Margin = new Thickness(0, 0, 0, 2);
            header.TextAlignment = TextAlignment.Center;
            header.Inlines.Add(new Bold(new Run("Shahjee super store")) { FontSize = 24 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 14 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("0332-3324911") { FontSize = 14 });
            doc.Blocks.Add(header);

            // --- BILL / INVOICE TITLE ---
            Paragraph titlePara = new Paragraph(new Bold(new Run(InvoiceTitle)))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 2, 0, 2)
            };
            doc.Blocks.Add(titlePara);

            // --- METADATA TABLE (Bill No, Date, etc.) ---
            Table metaTable = new Table() { CellSpacing = 0 };
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });

            TableRowGroup metaGroup = new TableRowGroup();

            // Compact cell builder so the metadata block stays tight (minimal vertical space).
            TableCell MetaCell(string text, int columnSpan = 1)
            {
                var cell = new TableCell(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
                {
                    Padding = new Thickness(0, 1, 0, 1)
                };
                if (columnSpan > 1) cell.ColumnSpan = columnSpan;
                return cell;
            }

            // Row 1: Bill No & Date (with time)
            TableRow row1 = new TableRow();
            row1.Cells.Add(MetaCell($"Bill No: {InvoiceNumber}"));
            row1.Cells.Add(MetaCell($"Date: {SaleDate:dd-MMM-yyyy hh:mm tt}"));
            metaGroup.Rows.Add(row1);

            // Row 2: Customer
            TableRow row2 = new TableRow();
            row2.Cells.Add(MetaCell($"Customer: {CustomerName}", columnSpan: 2));
            metaGroup.Rows.Add(row2);

            // Row 3: Address & Mobile
            TableRow row3 = new TableRow();
            row3.Cells.Add(MetaCell($"Address: {Address ?? ""}"));
            row3.Cells.Add(MetaCell($"Mobile: {MobileNumber ?? ""}"));
            metaGroup.Rows.Add(row3);

            // Row 4: Salesperson
            var salesperson = SessionManager.CurrentUser?.Username;
            if (!string.IsNullOrWhiteSpace(salesperson))
            {
                TableRow spRow = new TableRow();
                spRow.Cells.Add(MetaCell($"Salesperson: {salesperson}", columnSpan: 2));
                metaGroup.Rows.Add(spRow);
            }

            // Row 5: Bill Note (only printed when provided)
            if (!string.IsNullOrWhiteSpace(BillNote))
            {
                TableRow noteRow = new TableRow();
                noteRow.Cells.Add(MetaCell($"Note: {BillNote}", columnSpan: 2));
                metaGroup.Rows.Add(noteRow);
            }

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);


            // --- ITEMS TABLE ---
            Table itemsTable = new Table() { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            if (UseSmallBillFormat)
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.4, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.0, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.5, GridUnitType.Star) }); // Qty
                if (ShowCostPriceOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Cost
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Price
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.6, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1.0, GridUnitType.Star) }); // Total
            }
            else
            {
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.5, GridUnitType.Star) }); // S.No
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.5, GridUnitType.Star) }); // Product Name
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Qty
                if (ShowCostPriceOnReceipt)
                    itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Cost
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Price
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.8, GridUnitType.Star) }); // Disc
                itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Total
            }

            TableRowGroup itemsGroup = new TableRowGroup();

            // Compact, aligned cell builder for the items grid.
            TableCell ItemCell(string text, TextAlignment align)
            {
                return new TableCell(new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) })
                {
                    BorderThickness = new Thickness(0.5),
                    BorderBrush = Brushes.Black,
                    Padding = new Thickness(2, 1, 2, 1)
                };
            }

            // Header Row
            TableRow headerRow = new TableRow() { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            headerRow.Cells.Add(ItemCell("S.No", TextAlignment.Center));
            headerRow.Cells.Add(ItemCell("Product Name", TextAlignment.Left));
            headerRow.Cells.Add(ItemCell("Qty", TextAlignment.Center));
            if (ShowCostPriceOnReceipt)
                headerRow.Cells.Add(ItemCell("Cost", TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Price", TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Disc", TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Total", TextAlignment.Center));
            itemsGroup.Rows.Add(headerRow);

            // Item Rows
            int serialNo = 1;
            foreach (var item in SaleItems)
            {
                TableRow row = new TableRow();
                row.Cells.Add(ItemCell(serialNo++.ToString(), TextAlignment.Center));
                row.Cells.Add(ItemCell(item.ProductName, TextAlignment.Left));
                row.Cells.Add(ItemCell(item.Quantity.ToString(), TextAlignment.Center));
                if (ShowCostPriceOnReceipt)
                    row.Cells.Add(ItemCell(item.CostPrice.ToString("N0"), TextAlignment.Right));
                row.Cells.Add(ItemCell(item.UnitPrice.ToString("N0"), TextAlignment.Right));
                row.Cells.Add(ItemCell(item.EffectiveDiscountAmount > 0 ? item.EffectiveDiscountAmount.ToString("N0") : "", TextAlignment.Right));
                row.Cells.Add(ItemCell(item.Total.ToString("N2"), TextAlignment.Right));
                itemsGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // --- TOTALS SECTION ---
            // Use proportional (star) widths so the value column always fits within the
            // printable paper width. Fixed pixel widths (200 + 100) previously overflowed
            // the ~260px thermal paper, pushing the values column off the right edge and
            // printing blank totals.
            Table totalsTable = new Table() { CellSpacing = 0 };
            bool useSpacer = !UseSmallBillFormat;
            if (useSpacer)
                totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Left spacer (A4 only)
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(2, GridUnitType.Star) }); // Labels
            totalsTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) }); // Values

            TableRowGroup totalsGroup = new TableRowGroup();

            // Local helper keeps every row aligned and respects the format's column count.
            void AddTotalRow(string label, string value, bool bold = false, double fontSize = 12)
            {
                var row = new TableRow();
                if (useSpacer)
                    row.Cells.Add(new TableCell(new Paragraph(new Run(""))));

                var labelPara = new Paragraph(new Run(label)) { FontSize = fontSize, Margin = new Thickness(0) };
                if (bold) labelPara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(labelPara) { Padding = new Thickness(2, 1, 2, 1) });

                var valuePara = new Paragraph(new Run(value)) { TextAlignment = TextAlignment.Right, FontSize = fontSize, Margin = new Thickness(0) };
                if (bold) valuePara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(valuePara) { Padding = new Thickness(2, 1, 2, 1) });

                totalsGroup.Rows.Add(row);
            }

            if (TotalItemsDiscount > 0)
                AddTotalRow("Item Discounts", TotalItemsDiscount.ToString("N2"));
            var totalDisc = (DiscountOnBill ?? 0) + (DiscountOnProducts ?? 0) + TotalItemsDiscount;
            if (totalDisc > 0)
                AddTotalRow("Total Discount", totalDisc.ToString("N2"));
            AddTotalRow("Total Bill", TotalBill.ToString("N2"), bold: true);
            if (PreBalance > 0)
                AddTotalRow("Previous Balance", PreBalance.ToString("N2"));
            AddTotalRow("Cash Received", (ReceiveCash ?? 0).ToString("N2"), bold: true);
            AddTotalRow("Total Items Quantity", SaleItems.Sum(i => i.Quantity).ToString(), bold: true);
            AddTotalRow("Balance Amount", Balance.ToString("N2"), bold: true, fontSize: 14);

            totalsTable.RowGroups.Add(totalsGroup);
            doc.Blocks.Add(totalsTable);

            // --- FOOTER ---
            Paragraph footer = new Paragraph();
            footer.Margin = new Thickness(0, 5, 0, 0);
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
        private int _bonus;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private string _discountType = "%";
        private decimal _total;
        private string? _batchNo;
        private DateTime? _expiryDate;

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
                    CalculateTotal();
            }
        }

        public int Bonus
        {
            get => _bonus;
            set => SetProperty(ref _bonus, value);
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
                    CalculateTotal();
                    OnPropertyChanged(nameof(EffectiveDiscountAmount));
                    OnPropertyChanged(nameof(DiscountDisplay));
                }
            }
        }

        public string DiscountType
        {
            get => _discountType;
            set
            {
                if (SetProperty(ref _discountType, value))
                {
                    OnPropertyChanged(nameof(DiscountTypeIsPercent));
                    OnPropertyChanged(nameof(DiscountTypeIsPKR));
                    OnPropertyChanged(nameof(EffectiveDiscountAmount));
                    OnPropertyChanged(nameof(DiscountDisplay));
                    CalculateTotal();
                }
            }
        }

        public bool DiscountTypeIsPercent
        {
            get => _discountType == "%";
            set { if (value) DiscountType = "%"; }
        }

        public bool DiscountTypeIsPKR
        {
            get => _discountType == "PKR";
            set { if (value) DiscountType = "PKR"; }
        }

        public decimal EffectiveDiscountAmount =>
            _discountType == "%" ? (UnitPrice * Quantity * DiscountPercent) / 100 : DiscountPercent;

        public string DiscountDisplay =>
            _discountType == "%" ? $"{_discountPercent:N0}%" : $"Rs {_discountPercent:N0}";

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }

        public string? BatchNo
        {
            get => _batchNo;
            set => SetProperty(ref _batchNo, value);
        }

        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        private void CalculateTotal()
        {
            Total = _discountType == "%"
                ? (UnitPrice * Quantity) - ((UnitPrice * Quantity * DiscountPercent) / 100)
                : (UnitPrice * Quantity) - DiscountPercent;
        }
    }
}
