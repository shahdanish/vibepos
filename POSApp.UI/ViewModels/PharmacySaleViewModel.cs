using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class PharmacySaleViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;
        private readonly IPharmacyRepository _pharmacyRepository;
        private readonly IDoctorRepository _doctorRepository;

        // --- Header state ---
        private string _invoiceNumber = string.Empty;
        private DateTime _saleDate = DateTime.Now;
        private string _paymentType = "Cash";
        private string? _billNote;

        // --- Pharmacy dropdown ---
        private string _pharmacySearchText = string.Empty;
        private Pharmacy? _selectedPharmacy;
        private List<Pharmacy> _allPharmacies = new();

        // --- Doctor dropdown ---
        private string _doctorSearchText = string.Empty;
        private Doctor? _selectedDoctor;
        private List<Doctor> _allDoctors = new();

        // --- Product search ---
        private string _productSearchText = string.Empty;
        private List<Product> _allProductsList = new();

        // --- Product entry ---
        private string _barcodeInput = string.Empty;
        private Product? _selectedProduct;
        private int _quantity = 1;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private bool _autoAddItem = true;
        private bool _autoPrint = false;
        private bool _isSampleSale;
        private decimal _lastScannedCost;
        private bool _isLastScannedCostVisible;
        private readonly DispatcherTimer _costHideTimer;

        // --- Totals ---
        private decimal _discountOnBill;
        private decimal _totalBill;
        private decimal _receiveCash;
        private decimal _balance;

        // ----------------------------------------------------------------
        // Collections
        // ----------------------------------------------------------------
        public ObservableCollection<SaleItemViewModel> SaleItems { get; } = new();
        public ObservableCollection<Product> AllProducts { get; } = new();
        public ObservableCollection<Product> FilteredProducts { get; } = new();
        public ObservableCollection<Pharmacy> FilteredPharmacies { get; } = new();
        public ObservableCollection<Doctor> FilteredDoctors { get; } = new();

        public IReadOnlyList<string> PaymentTypes { get; } =
            new[] { "Cash", "Credit Card", "Bank Transfer" };

        // ----------------------------------------------------------------
        // Header properties
        // ----------------------------------------------------------------
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

        public string? BillNote
        {
            get => _billNote;
            set => SetProperty(ref _billNote, value);
        }

        // ----------------------------------------------------------------
        // Pharmacy dropdown
        // ----------------------------------------------------------------
        public string PharmacySearchText
        {
            get => _pharmacySearchText;
            set
            {
                if (SetProperty(ref _pharmacySearchText, value))
                    FilterPharmacies(value);
            }
        }

        public Pharmacy? SelectedPharmacy
        {
            get => _selectedPharmacy;
            set
            {
                if (SetProperty(ref _selectedPharmacy, value) && value != null)
                    _pharmacySearchText = value.DisplayLabel; // keep text in sync
            }
        }

        // ----------------------------------------------------------------
        // Doctor dropdown
        // ----------------------------------------------------------------
        public string DoctorSearchText
        {
            get => _doctorSearchText;
            set
            {
                if (SetProperty(ref _doctorSearchText, value))
                    FilterDoctors(value);
            }
        }

        public Doctor? SelectedDoctor
        {
            get => _selectedDoctor;
            set
            {
                if (SetProperty(ref _selectedDoctor, value) && value != null)
                    _doctorSearchText = value.DisplayLabel;
            }
        }

        // ----------------------------------------------------------------
        // Product search
        // ----------------------------------------------------------------
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                    FilterProducts(value);
            }
        }

        // ----------------------------------------------------------------
        // Product entry
        // ----------------------------------------------------------------
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
                    _productSearchText = value.ProductName;
                    UnitPrice = value.UnitPrice;
                    if (AutoAddItem)
                    {
                        try { AddItem(); } catch { }
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

        public bool AutoAddItem
        {
            get => _autoAddItem;
            set
            {
                if (SetProperty(ref _autoAddItem, value))
                    SettingsManager.SaveSetting(s => s.AutoAddItem = value);
            }
        }

        public bool AutoPrint
        {
            get => _autoPrint;
            set
            {
                if (SetProperty(ref _autoPrint, value))
                    SettingsManager.SaveSetting(s => s.AutoPrint = value);
            }
        }

        public bool IsSampleSale
        {
            get => _isSampleSale;
            set => SetProperty(ref _isSampleSale, value);
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

        // ----------------------------------------------------------------
        // Totals
        // ----------------------------------------------------------------
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
                    Balance = ReceiveCash - TotalBill;
            }
        }

        public decimal Balance
        {
            get => _balance;
            set => SetProperty(ref _balance, value);
        }

        // ----------------------------------------------------------------
        // Commands
        // ----------------------------------------------------------------
        public ICommand ScanCommand { get; }
        public ICommand AddItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand NewSaleCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand AddPharmacyCommand { get; }
        public ICommand AddDoctorCommand { get; }
        public ICommand AddProductCommand { get; }

        // Raised by the view to open Add Pharmacy / Add Doctor / Add Product dialogs
        public event Action? OpenAddPharmacyRequested;
        public event Action? OpenAddDoctorRequested;
        public event Action? OpenAddProductRequested;

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public PharmacySaleViewModel(
            ISaleRepository saleRepository,
            IProductRepository productRepository,
            IPharmacyRepository pharmacyRepository,
            IDoctorRepository doctorRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;
            _pharmacyRepository = pharmacyRepository;
            _doctorRepository = doctorRepository;

            var settings = SettingsManager.LoadSettings();
            _autoPrint = settings.AutoPrint;
            _autoAddItem = settings.AutoAddItem;

            _costHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _costHideTimer.Tick += (_, _) => { IsLastScannedCostVisible = false; _costHideTimer.Stop(); };

            SaleItems.CollectionChanged += (_, e) =>
            {
                if (e.OldItems != null)
                    foreach (SaleItemViewModel i in e.OldItems)
                        i.PropertyChanged -= OnItemChanged;
                if (e.NewItems != null)
                    foreach (SaleItemViewModel i in e.NewItems)
                        i.PropertyChanged += OnItemChanged;
                CalculateTotals();
            };

            ScanCommand = new RelayCommand(async _ => await ProcessBarcodeScanAsync());
            AddItemCommand = new RelayCommand(_ => AddItem());
            DeleteItemCommand = new RelayCommand(p => { if (p is SaleItemViewModel i) { SaleItems.Remove(i); CalculateTotals(); } });
            SaveCommand = new RelayCommand(async _ => await SaveSaleAsync());
            PrintCommand = new RelayCommand(async _ => await PrintAndSaveAsync());
            NewSaleCommand = new RelayCommand(_ => StartNewSale());
            ClearCommand = new RelayCommand(_ => ConfirmClear());
            AddPharmacyCommand = new RelayCommand(_ => OpenAddPharmacyRequested?.Invoke());
            AddDoctorCommand = new RelayCommand(_ => OpenAddDoctorRequested?.Invoke());
            AddProductCommand = new RelayCommand(_ => OpenAddProductRequested?.Invoke());

            _ = LoadDataAsync();
        }

        // ----------------------------------------------------------------
        // Data loading
        // ----------------------------------------------------------------
        public async Task LoadDataAsync()
        {
            InvoiceNumber = await _saleRepository.GetNextInvoiceNumberAsync();

            var products = await _productRepository.GetAllAsync();
            _allProductsList = products.ToList();
            AllProducts.Clear();
            foreach (var p in _allProductsList) AllProducts.Add(p);
            RebuildFilteredProducts(_allProductsList);

            _allPharmacies = (await _pharmacyRepository.GetAllAsync(includeInactive: false)).ToList();
            _allDoctors = (await _doctorRepository.GetAllAsync(includeInactive: false)).ToList();

            RebuildFilteredPharmacies(_allPharmacies);
            RebuildFilteredDoctors(_allDoctors);
        }

        public async Task ReloadPharmaciesAsync()
        {
            _allPharmacies = (await _pharmacyRepository.GetAllAsync(includeInactive: false)).ToList();
            FilterPharmacies(PharmacySearchText);
        }

        public async Task ReloadDoctorsAsync()
        {
            _allDoctors = (await _doctorRepository.GetAllAsync(includeInactive: false)).ToList();
            FilterDoctors(DoctorSearchText);
        }

        public async Task ReloadProductsAsync()
        {
            var products = await _productRepository.GetAllAsync();
            _allProductsList = products.ToList();
            AllProducts.Clear();
            foreach (var p in _allProductsList) AllProducts.Add(p);
            FilterProducts(ProductSearchText);
        }

        // ----------------------------------------------------------------
        // Filtering helpers
        // ----------------------------------------------------------------
        private void FilterPharmacies(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                RebuildFilteredPharmacies(_allPharmacies);
                return;
            }
            var lower = search.ToLower();
            var filtered = _allPharmacies.Where(p =>
                p.Name.ToLower().Contains(lower) ||
                (p.Area != null && p.Area.ToLower().Contains(lower)));
            RebuildFilteredPharmacies(filtered);
        }

        private void FilterDoctors(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                RebuildFilteredDoctors(_allDoctors);
                return;
            }
            var lower = search.ToLower();
            var filtered = _allDoctors.Where(d =>
                d.Name.ToLower().Contains(lower) ||
                (d.Specialization != null && d.Specialization.ToLower().Contains(lower)));
            RebuildFilteredDoctors(filtered);
        }

        private void RebuildFilteredPharmacies(IEnumerable<Pharmacy> source)
        {
            FilteredPharmacies.Clear();
            foreach (var p in source) FilteredPharmacies.Add(p);
        }

        private void RebuildFilteredDoctors(IEnumerable<Doctor> source)
        {
            FilteredDoctors.Clear();
            foreach (var d in source) FilteredDoctors.Add(d);
        }

        private void FilterProducts(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                RebuildFilteredProducts(_allProductsList);
                return;
            }
            var lower = search.ToLower();
            var filtered = _allProductsList.Where(p =>
                p.ProductName.ToLower().Contains(lower) ||
                (p.ProductId != null && p.ProductId.ToLower().Contains(lower)));
            RebuildFilteredProducts(filtered);
        }

        private void RebuildFilteredProducts(IEnumerable<Product> source)
        {
            FilteredProducts.Clear();
            foreach (var p in source) FilteredProducts.Add(p);
        }

        // ----------------------------------------------------------------
        // Barcode scan
        // ----------------------------------------------------------------
        private async Task ProcessBarcodeScanAsync()
        {
            var barcode = BarcodeInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(barcode)) return;

            try
            {
                var product = await _productRepository.GetByBarcodeAsync(barcode)
                              ?? await _productRepository.GetByProductIdAsync(barcode);

                if (product == null)
                {
                    NotificationHelper.ShowError($"No product found for code '{barcode}'.");
                    BarcodeInput = string.Empty;
                    return;
                }
                if (product.IsDeleted)
                {
                    NotificationHelper.ShowError($"'{product.ProductName}' is deleted and cannot be sold.");
                    BarcodeInput = string.Empty;
                    return;
                }
                if (product.Stock <= 0)
                {
                    NotificationHelper.ShowError($"'{product.ProductName}' is out of stock!");
                    BarcodeInput = string.Empty;
                    return;
                }

                var existing = SaleItems.FirstOrDefault(i => i.ProductId == product.ProductId);
                if (existing != null)
                {
                    existing.Quantity += 1;
                    CalculateTotals();
                }
                else
                {
                    var item = new SaleItemViewModel
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Quantity = 1,
                        CostPrice = product.CostPrice,
                        UnitPrice = product.UnitPrice,
                        DiscountPercent = 0,
                        Total = product.UnitPrice,
                        BatchNo = product.BatchNo,
                        ExpiryDate = product.ExpiryDate
                    };
                    SaleItems.Add(item);
                    ShowCost(product.CostPrice);
                }

                BarcodeInput = string.Empty;
            }
            catch { BarcodeInput = string.Empty; }
        }

        // ----------------------------------------------------------------
        // Add item from dropdown
        // ----------------------------------------------------------------
        private void AddItem()
        {
            if (SelectedProduct == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a product before adding.");
                return;
            }
            if (Quantity <= 0) Quantity = 1;

            var existing = SaleItems.FirstOrDefault(i => i.ProductId == SelectedProduct.ProductId);
            if (existing != null)
            {
                existing.Quantity += Quantity;
            }
            else
            {
                var total = (UnitPrice * Quantity) * (1 - DiscountPercent / 100);
                SaleItems.Add(new SaleItemViewModel
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    Quantity = Quantity,
                    CostPrice = SelectedProduct.CostPrice,
                    UnitPrice = UnitPrice,
                    DiscountPercent = DiscountPercent,
                    Total = total,
                    BatchNo = SelectedProduct.BatchNo,
                    ExpiryDate = SelectedProduct.ExpiryDate
                });
            }

            CalculateTotals();
            ShowCost(SelectedProduct.CostPrice);

            SelectedProduct = null;
            Quantity = 1;
            UnitPrice = 0;
            DiscountPercent = 0;
            OnPropertyChanged(nameof(SaleItems));
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SaleItemViewModel.Total)
                or nameof(SaleItemViewModel.Quantity)
                or nameof(SaleItemViewModel.UnitPrice)
                or nameof(SaleItemViewModel.DiscountPercent))
                CalculateTotals();
        }

        private void CalculateTotals()
        {
            var subtotal = SaleItems.Sum(i => i.Total);
            TotalBill = subtotal - DiscountOnBill;
            ReceiveCash = TotalBill;
            Balance = ReceiveCash - TotalBill;
        }

        private void ShowCost(decimal cost)
        {
            LastScannedCost = cost;
            IsLastScannedCostVisible = true;
            _costHideTimer.Stop();
            _costHideTimer.Start();
        }

        // ----------------------------------------------------------------
        // Save
        // ----------------------------------------------------------------
        private async Task SaveSaleAsync(bool printFirst = false)
        {
            if (SelectedPharmacy == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a pharmacy before saving the sale.");
                return;
            }
            if (!SaleItems.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please add at least one product before saving.");
                return;
            }
            if (SelectedDoctor == null)
            {
                if (!NotificationHelper.Confirm("No doctor selected. Continue without a doctor?", "No Doctor Selected"))
                    return;
            }

            try
            {
                if (printFirst) DoPrint();

                var sale = new Sale
                {
                    InvoiceNumber = InvoiceNumber,
                    SaleDate = SaleDate,
                    SaleType = "PharmacySale",
                    PaymentType = PaymentType,
                    CustomerName = SelectedPharmacy.Name,
                    PharmacyId = SelectedPharmacy.Id,
                    DoctorId = SelectedDoctor?.Id,
                    BillNote = BillNote,
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
                        Bonus = item.Bonus,
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        Total = item.Total
                    });
                }

                await _saleRepository.AddAsync(sale);

                foreach (var item in SaleItems)
                {
                    var product = AllProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - (item.Quantity + item.Bonus));
                        await _productRepository.UpdateAsync(product);
                    }
                }

                if (AutoPrint)
                    DoPrint();

                StartNewSale();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save pharmacy sale", ex.Message);
            }
        }

        private async Task PrintAndSaveAsync() => await SaveSaleAsync(printFirst: true);

        // ----------------------------------------------------------------
        // Clear / New sale
        // ----------------------------------------------------------------
        private void ConfirmClear()
        {
            if (!SaleItems.Any() && SelectedPharmacy == null && SelectedDoctor == null)
            {
                StartNewSale();
                return;
            }
            if (NotificationHelper.Confirm(
                    "Clear all items and selections? This cannot be undone.",
                    "Clear Sale"))
                StartNewSale();
        }

        public void StartNewSale()
        {
            SaleItems.Clear();
            SelectedPharmacy = null;
            SelectedDoctor = null;
            PharmacySearchText = string.Empty;
            DoctorSearchText = string.Empty;
            PaymentType = "Cash";
            BillNote = null;
            DiscountOnBill = 0;
            TotalBill = 0;
            ReceiveCash = 0;
            Balance = 0;
            BarcodeInput = string.Empty;
            LastScannedCost = 0;
            IsLastScannedCostVisible = false;
            IsSampleSale = false;
            _ = LoadDataAsync();
        }

        // ----------------------------------------------------------------
        // Print
        // ----------------------------------------------------------------
        private bool DoPrint()
        {
            try
            {
                var dlg = new System.Windows.Controls.PrintDialog();
                if (dlg.ShowDialog() != true) return false;
                double pageW = dlg.PrintableAreaWidth > 0 ? dlg.PrintableAreaWidth : 793.7;
                double pageH = dlg.PrintableAreaHeight > 0 ? dlg.PrintableAreaHeight : 1122.5;

                var fixedDoc = new FixedDocument();
                fixedDoc.DocumentPaginator.PageSize = new Size(pageW, pageH);

                var fixedPage = new FixedPage { Width = pageW, Height = pageH, Background = Brushes.White };
                var invoicePage = BuildInvoicePage(pageW, pageH);
                FixedPage.SetLeft(invoicePage, 0);
                FixedPage.SetTop(invoicePage, 0);
                fixedPage.Children.Add(invoicePage);

                fixedPage.Measure(new Size(pageW, pageH));
                fixedPage.Arrange(new Rect(0, 0, pageW, pageH));
                fixedPage.UpdateLayout();

                var pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);
                fixedDoc.Pages.Add(pageContent);

                dlg.PrintDocument(fixedDoc.DocumentPaginator, "Pharmacy Invoice");
                return true;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print pharmacy invoice", ex.Message);
                return false;
            }
        }

        // ── Invoice visual builder ────────────────────────────────────────
        private Grid BuildInvoicePage(double pageW, double pageH)
        {
            var F = new FontFamily("Arial");
            const double mH = 45, mTop = 28, mBot = 20;
            double cW = pageW - mH * 2;
            var ph = SelectedPharmacy;
            string userName = (SessionManager.CurrentUser?.Username ?? "ADMIN").ToUpper();

            // ── helpers ───────────────────────────────────────────────────
            TextBlock MkTB(string text, double sz = 10, bool bold = false,
                           TextAlignment al = TextAlignment.Left, double mB = 0) =>
                new TextBlock
                {
                    Text = text, FontFamily = F, FontSize = sz,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                    TextAlignment = al, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, mB)
                };

            Border HLine(double t = 0.6, double mT = 3, double mB = 3) =>
                new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0, t, 0, 0),
                    Margin = new Thickness(0, mT, 0, mB)
                };

            // Proper 2-column label:value row for meta section
            Grid MetaRow(string lbl, string val)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var lt = new TextBlock { Text = lbl, FontFamily = F, FontSize = 10.5, FontWeight = FontWeights.Bold };
                var vt = new TextBlock { Text = val, FontFamily = F, FontSize = 10.5, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(vt, 1);
                g.Children.Add(lt); g.Children.Add(vt);
                return g;
            }

            // Self-contained row for items table — one Grid per row
            // Columns: Sr | Name(*) | Qty | Bon | Disc% | Batch | Exp | Price | Total
            double[] cws = { 26, 0, 34, 34, 42, 62, 52, 68, 68 };
            Grid TblRow(bool hdr, string c0, string c1, string c2, string c3,
                        string c4, string c5, string c6, string c7, string c8)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[0]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[2]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[3]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[4]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[5]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[6]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[7]) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[8]) });
                if (hdr) g.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                var bl = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, hdr ? 0.8 : 0.4) };
                Grid.SetColumnSpan(bl, 9); g.Children.Add(bl);
                var texts = new[] { c0, c1, c2, c3, c4, c5, c6, c7, c8 };
                var aligns = new[] { TextAlignment.Center, TextAlignment.Left, TextAlignment.Center, TextAlignment.Center,
                                     TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
                                     TextAlignment.Right, TextAlignment.Right };
                for (int i = 0; i < 9; i++)
                {
                    var tb = new TextBlock
                    {
                        Text = texts[i], FontFamily = F,
                        FontSize = hdr ? 11 : 10,
                        FontWeight = hdr ? FontWeights.Bold : FontWeights.Normal,
                        TextAlignment = aligns[i],
                        Padding = new Thickness(3, 2, 3, 2),
                        TextWrapping = TextWrapping.NoWrap
                    };
                    Grid.SetColumn(tb, i); g.Children.Add(tb);
                }
                return g;
            }

            // ── ROOT GRID: content at top, spacer, footer pinned to bottom ─
            var root = new Grid { Width = pageW, Height = pageH, Background = Brushes.White };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 0: content
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: spacer
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                      // 2: footer

            // ── CONTENT ───────────────────────────────────────────────────
            var content = new StackPanel { Width = cW, Margin = new Thickness(mH, mTop, mH, 0) };
            Grid.SetRow(content, 0);
            root.Children.Add(content);

            // Page label
            content.Children.Add(MkTB("Page 1 of 1", 7.5, false, TextAlignment.Right, 2));

            // ── HEADER ────────────────────────────────────────────────────
            var hdrSP = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
            hdrSP.Children.Add(MkTB("Invoice", 10.5, false, TextAlignment.Center, 2));
            hdrSP.Children.Add(new TextBlock
            {
                Text = "Master Pharmaceuticals Distributor",
                FontFamily = F, FontSize = 20, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });
            hdrSP.Children.Add(MkTB("Office #410, 4th Floor Kohistan Tower", 10.5, false, TextAlignment.Center));
            hdrSP.Children.Add(MkTB("Saddar, Rawalpindi", 10.5, false, TextAlignment.Center, 2));
            hdrSP.Children.Add(MkTB("NTN#G985456   DSL # 374-89100937-2025", 10, false, TextAlignment.Center));
            content.Children.Add(hdrSP);

            content.Children.Add(HLine(0.7, 0, 0));

            // ── PHARMACY SECTION ──────────────────────────────────────────
            if (ph != null)
            {
                var phSP = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
                var phTB = new TextBlock { FontFamily = F, FontSize = 10.5, TextWrapping = TextWrapping.Wrap };
                if (!string.IsNullOrWhiteSpace(ph.Area))
                    phTB.Inlines.Add(new System.Windows.Documents.Run($"Alias: {ph.Area}    "));
                phTB.Inlines.Add(new System.Windows.Documents.Run("M/s: "));
                phTB.Inlines.Add(new System.Windows.Documents.Run(ph.Name) { FontWeight = FontWeights.Bold });
                if (!string.IsNullOrWhiteSpace(ph.Address))
                    phTB.Inlines.Add(new System.Windows.Documents.Run($"      {ph.Address}"));
                phSP.Children.Add(phTB);

                var licParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(ph.LicenseNo)) licParts.Add($"LICENSE NO  {ph.LicenseNo}");
                if (!string.IsNullOrWhiteSpace(ph.Ntn)) licParts.Add($"NTN NO #{ph.Ntn}");
                if (licParts.Any()) phSP.Children.Add(MkTB(string.Join("        ", licParts), 10));

                content.Children.Add(phSP);
            }

            content.Children.Add(HLine(0.5, 4, 0));

            // ── META TWO-COLUMN ───────────────────────────────────────────
            var metaG = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            metaG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metaG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftMeta = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
            leftMeta.Children.Add(MetaRow("Date:", $"{SaleDate:dd/MM/yyyy  HH:mm:ss}"));
            leftMeta.Children.Add(MetaRow("Invoice No.:", InvoiceNumber));
            leftMeta.Children.Add(MetaRow("User:", userName));
            leftMeta.Children.Add(MetaRow("Ref.:", BillNote ?? ""));
            if (SelectedDoctor != null)
                leftMeta.Children.Add(MetaRow("Doctor:", SelectedDoctor.Name +
                    (string.IsNullOrWhiteSpace(SelectedDoctor.Specialization) ? "" : $"  ({SelectedDoctor.Specialization})")));
            leftMeta.Children.Add(MetaRow("Remarks:", ""));
            Grid.SetColumn(leftMeta, 0);
            metaG.Children.Add(leftMeta);

            var rightMeta = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            rightMeta.Children.Add(MkTB($"NTN:   {ph?.Ntn ?? ""}", 10.5, false, TextAlignment.Right, 2));
            rightMeta.Children.Add(MkTB($"Sales Person:   {userName}", 10.5, false, TextAlignment.Right, 2));
            Grid.SetColumn(rightMeta, 1);
            metaG.Children.Add(rightMeta);
            content.Children.Add(metaG);

            content.Children.Add(HLine(0.7, 0, 0));

            // ── ITEMS TABLE (StackPanel of per-row Grids) ─────────────────
            var tblSP = new StackPanel { Width = cW };
            tblSP.Children.Add(TblRow(true, "Sr.#", "Item Name", "Qty", "Bon.", "Disc%", "Batch", "Expiry", "Sale Price", "Total"));

            int sr = 1, totalQty = 0, totalBonus = 0;
            foreach (var item in SaleItems)
            {
                totalQty += item.Quantity;
                totalBonus += item.Bonus;
                tblSP.Children.Add(TblRow(false,
                    sr++.ToString(),
                    item.ProductName,
                    item.Quantity.ToString(),
                    item.Bonus > 0 ? item.Bonus.ToString() : "",
                    item.DiscountPercent > 0 ? item.DiscountPercent.ToString("N1") + "%" : "",
                    item.BatchNo ?? "",
                    item.ExpiryDate?.ToString("MM/yy") ?? "",
                    item.UnitPrice.ToString("N2"),
                    item.Total.ToString("N2")));
            }

            // Gross total summary row — mirrors the 9-column layout
            var grossG = new Grid { Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)) };
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[0]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[2]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[3]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[4]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[5]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[6]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[7]) });
            grossG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cws[8]) });
            var grossLine = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 0, 0, 0.6) };
            Grid.SetColumnSpan(grossLine, 9); grossG.Children.Add(grossLine);
            var grossLbl = new TextBlock { Text = "Gross Total:", FontFamily = F, FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, Padding = new Thickness(3, 2, 4, 2) };
            Grid.SetColumn(grossLbl, 1); grossG.Children.Add(grossLbl);
            var grossQtyTB = new TextBlock { Text = totalQty.ToString(), FontFamily = F, FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Padding = new Thickness(3, 2, 3, 2) };
            Grid.SetColumn(grossQtyTB, 2); grossG.Children.Add(grossQtyTB);
            if (totalBonus > 0)
            {
                var grossBonTB = new TextBlock { Text = totalBonus.ToString(), FontFamily = F, FontSize = 10, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Padding = new Thickness(3, 2, 3, 2) };
                Grid.SetColumn(grossBonTB, 3); grossG.Children.Add(grossBonTB);
            }
            tblSP.Children.Add(grossG);
            content.Children.Add(tblSP);

            // ── NET TOTAL ─────────────────────────────────────────────────
            var netG = new Grid { Margin = new Thickness(0, 6, 0, 4) };
            netG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            netG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            netG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (IsSampleSale)
            {
                var sampleLbl = new TextBlock
                {
                    Text = "SAMPLE PRODUCT",
                    FontFamily = F,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(sampleLbl, 0);
                netG.Children.Add(sampleLbl);
            }
            var netLbl = new TextBlock { Text = "Net Total", FontFamily = F, FontSize = 15, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            Grid.SetColumn(netLbl, 1); netG.Children.Add(netLbl);
            var netBox = new Border
            {
                BorderBrush = Brushes.Black, BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Child = new TextBlock { Text = $"Rs. {TotalBill:N2}", FontFamily = F, FontSize = 15, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Right, MinWidth = 100 }
            };
            Grid.SetColumn(netBox, 2); netG.Children.Add(netBox);
            content.Children.Add(netG);

            content.Children.Add(HLine(0.4, 0, 3));
            content.Children.Add(MkTB($"Total Items: {SaleItems.Count}    |    {AmountInWords((long)Math.Round(TotalBill))} RUPEES ONLY", 10.5, true, TextAlignment.Left));

            // ── FOOTER (pinned to absolute bottom via * spacer row) ───────
            var footer = new StackPanel { Width = cW, Margin = new Thickness(mH, 0, mH, mBot) };
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            // ── STAMP AREA (above warranty) ───────────────────────────────
            var stampGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            stampGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stampGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            var stampSP = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };
            stampSP.Children.Add(new Border { Height = 36 }); // blank space for stamp impression
            stampSP.Children.Add(new Border
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 0, 0, 4)
            });
            stampSP.Children.Add(new TextBlock
            {
                Text = "Master Pharmaceutical Distributor",
                FontFamily = F, FontSize = 10, FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
            Grid.SetColumn(stampSP, 1);
            stampGrid.Children.Add(stampSP);
            footer.Children.Add(stampGrid);

            footer.Children.Add(HLine(0.6, 0, 5));
            footer.Children.Add(MkTB("Warranty under section 23(1)(i) of the Drug Act 1976.", 7.5, true, TextAlignment.Left, 2));
            footer.Children.Add(new TextBlock
            {
                FontFamily = F, FontSize = 7, FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 3),
                Text = "I Momina Haroon being a person resident in pakistan, carrying on business at office # 410 4th Floor " +
                       "Kohistan Tower Rwp. Under the name of Master pharmaceutical Distributor do hearby give this warranty " +
                       "that the drugs here-under described as sold / indented by me / specified and contained in the bill " +
                       "of sale invoice, bill of landing or other documents describing the goods referred to herein do not " +
                       "contravene in any way the provisions of section 23 of the Drugs Act, 1976."
            });
            footer.Children.Add(MkTB("Terms:", 7.5, true, TextAlignment.Left, 4));
            footer.Children.Add(MkTB("Software from Husain Software  Cell 03137643443", 7, false, TextAlignment.Center));

            return root;
        }

        private FlowDocument CreatePharmacyInvoice()
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Arial"),
                FontSize = 10,
                TextAlignment = TextAlignment.Left
            };

            // ── Page 1 of 1 ──────────────────────────────────────────────
            var pageLabel = new Paragraph(new Run("Page 1 of 1"))
            {
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 0),
                FontSize = 8
            };
            doc.Blocks.Add(pageLabel);

            // ── HARDCODED HEADER ─────────────────────────────────────────
            var hdr = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
            hdr.Inlines.Add(new Run("Invoice") { FontSize = 10 });
            hdr.Inlines.Add(new LineBreak());
            hdr.Inlines.Add(new Bold(new Run("Master Pharmaceuticals Distributor")) { FontSize = 18 });
            hdr.Inlines.Add(new LineBreak());
            hdr.Inlines.Add(new Run("Office #410, 4th Floor Kohistan Tower") { FontSize = 9 });
            hdr.Inlines.Add(new LineBreak());
            hdr.Inlines.Add(new Run("Saddar, Rawalpindi") { FontSize = 9 });
            hdr.Inlines.Add(new LineBreak());
            hdr.Inlines.Add(new Run("NTN#G985456  DSL # 374-89100937-2025") { FontSize = 9 });
            doc.Blocks.Add(hdr);

            // ── PHARMACY DETAILS (dynamic) ────────────────────────────────
            var ph = SelectedPharmacy;
            if (ph != null)
            {
                var phPara = new Paragraph { Margin = new Thickness(0, 4, 0, 2), FontSize = 9, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(0, 2, 0, 2) };
                string alias = string.IsNullOrWhiteSpace(ph.Area) ? "" : $"Alias: {ph.Area}   ";
                phPara.Inlines.Add(new Bold(new Run($"{alias}M/s: {ph.Name}")));
                if (!string.IsNullOrWhiteSpace(ph.Address))
                {
                    phPara.Inlines.Add(new Run($"   {ph.Address}"));
                }
                if (!string.IsNullOrWhiteSpace(ph.LicenseNo) || !string.IsNullOrWhiteSpace(ph.Ntn))
                {
                    phPara.Inlines.Add(new LineBreak());
                    var licNtn = new List<string>();
                    if (!string.IsNullOrWhiteSpace(ph.LicenseNo)) licNtn.Add($"LICENSE NO {ph.LicenseNo}");
                    if (!string.IsNullOrWhiteSpace(ph.Ntn)) licNtn.Add($"NTN NO #{ph.Ntn}");
                    phPara.Inlines.Add(new Run(string.Join("   ", licNtn)));
                }
                doc.Blocks.Add(phPara);
            }

            // ── META TABLE (Date/Invoice left | NTN/Salesperson right) ───
            var metaTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 2, 0, 2) };
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            var metaGrp = new TableRowGroup();

            TableCell MetaCell(Block b) =>
                new TableCell(b) { Padding = new Thickness(0, 1, 4, 1) };

            var leftMeta = new Paragraph { FontSize = 9, Margin = new Thickness(0) };
            leftMeta.Inlines.Add(new Run($"Date:\t{SaleDate:dd/MM/yyyy HH:mm:ss}"));
            leftMeta.Inlines.Add(new LineBreak());
            leftMeta.Inlines.Add(new Run($"Invoice No.:\t{InvoiceNumber}"));
            leftMeta.Inlines.Add(new LineBreak());
            leftMeta.Inlines.Add(new Run($"User:\t{SessionManager.CurrentUser?.Username?.ToUpper()}"));
            leftMeta.Inlines.Add(new LineBreak());
            leftMeta.Inlines.Add(new Run($"Ref.:\t{BillNote ?? ""}"));
            if (SelectedDoctor != null)
            {
                leftMeta.Inlines.Add(new LineBreak());
                leftMeta.Inlines.Add(new Run($"Doctor:\t{SelectedDoctor.Name}"));
            }
            leftMeta.Inlines.Add(new LineBreak());
            leftMeta.Inlines.Add(new Run("Remarks:"));

            var rightMeta = new Paragraph { FontSize = 9, Margin = new Thickness(0), TextAlignment = TextAlignment.Right };
            rightMeta.Inlines.Add(new Run($"NTN:  {ph?.Ntn ?? ""}"));
            rightMeta.Inlines.Add(new LineBreak());
            rightMeta.Inlines.Add(new Run($"Sales Person:  {SessionManager.CurrentUser?.Username?.ToUpper()}"));

            var metaRow = new TableRow();
            metaRow.Cells.Add(MetaCell(leftMeta));
            metaRow.Cells.Add(MetaCell(rightMeta));
            metaGrp.Rows.Add(metaRow);
            metaTable.RowGroups.Add(metaGrp);
            doc.Blocks.Add(metaTable);

            // ── ITEMS TABLE ───────────────────────────────────────────────
            var tbl = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 4, 0, 0) };
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.35, GridUnitType.Star) }); // Sr#
            tbl.Columns.Add(new TableColumn { Width = new GridLength(2.8, GridUnitType.Star) });  // Item Name
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.45, GridUnitType.Star) }); // Qty
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.45, GridUnitType.Star) }); // Bon.
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });  // Batch
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.7, GridUnitType.Star) });  // Expiry
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });  // Sale Price
            tbl.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });  // Total

            TableCell TC(string t, TextAlignment a = TextAlignment.Left, bool bold = false, bool border = true)
            {
                var p = new Paragraph(new Run(t)) { TextAlignment = a, Margin = new Thickness(0), FontSize = 9 };
                if (bold) p.FontWeight = FontWeights.Bold;
                var cell = new TableCell(p) { Padding = new Thickness(2, 1, 2, 1) };
                if (border) { cell.BorderBrush = Brushes.Black; cell.BorderThickness = new Thickness(0, 0, 0, 0.5); }
                return cell;
            }

            var tblGrp = new TableRowGroup();

            // Header row
            var tblHdr = new TableRow { Background = Brushes.White };
            tblHdr.Cells.Add(TC("Sr#", TextAlignment.Center, true));
            tblHdr.Cells.Add(TC("Item Name", TextAlignment.Left, true));
            tblHdr.Cells.Add(TC("Qty", TextAlignment.Center, true));
            tblHdr.Cells.Add(TC("Bon.", TextAlignment.Center, true));
            tblHdr.Cells.Add(TC("Batch", TextAlignment.Center, true));
            tblHdr.Cells.Add(TC("Expiry", TextAlignment.Center, true));
            tblHdr.Cells.Add(TC("Sale Price", TextAlignment.Right, true));
            tblHdr.Cells.Add(TC("Total", TextAlignment.Right, true));
            tblGrp.Rows.Add(tblHdr);

            int sr = 1;
            int totalQty = 0;
            foreach (var item in SaleItems)
            {
                totalQty += item.Quantity;
                var row = new TableRow();
                row.Cells.Add(TC(sr++.ToString(), TextAlignment.Center));
                row.Cells.Add(TC(item.ProductName, TextAlignment.Left));
                row.Cells.Add(TC(item.Quantity.ToString(), TextAlignment.Center));
                row.Cells.Add(TC(item.Bonus > 0 ? item.Bonus.ToString() : "", TextAlignment.Center));
                row.Cells.Add(TC(item.BatchNo ?? "", TextAlignment.Center));
                row.Cells.Add(TC(item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("MM/yy") : "", TextAlignment.Center));
                row.Cells.Add(TC(item.UnitPrice.ToString("N2"), TextAlignment.Right));
                row.Cells.Add(TC(item.Total.ToString("N2"), TextAlignment.Right));
                tblGrp.Rows.Add(row);
            }

            // Gross Total row
            var grossRow = new TableRow { FontWeight = FontWeights.Bold };
            grossRow.Cells.Add(TC("", TextAlignment.Left, false, false));
            grossRow.Cells.Add(TC("Gross Total:", TextAlignment.Right, true, false));
            grossRow.Cells.Add(TC(totalQty.ToString(), TextAlignment.Center, true, false));
            grossRow.Cells.Add(TC("", TextAlignment.Center, false, false));
            grossRow.Cells.Add(TC("", TextAlignment.Center, false, false));
            grossRow.Cells.Add(TC("", TextAlignment.Center, false, false));
            grossRow.Cells.Add(TC("", TextAlignment.Right, false, false));
            grossRow.Cells.Add(TC("", TextAlignment.Right, false, false));
            tblGrp.Rows.Add(grossRow);

            tbl.RowGroups.Add(tblGrp);
            doc.Blocks.Add(tbl);

            // ── NET TOTAL ─────────────────────────────────────────────────
            var netPara = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 0), BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 3, 0, 0) };
            netPara.Inlines.Add(new Bold(new Run($"Net Total.")) { FontSize = 12 });
            netPara.Inlines.Add(new Run("      "));
            netPara.Inlines.Add(new Bold(new Run($"{TotalBill:N2}")) { FontSize = 12 });
            doc.Blocks.Add(netPara);

            // ── ITEM COUNT + AMOUNT IN WORDS ──────────────────────────────
            var wordsPara = new Paragraph { Margin = new Thickness(0, 4, 0, 0), FontSize = 9 };
            wordsPara.Inlines.Add(new Run($"Total No. of Items:  {SaleItems.Count}"));
            wordsPara.Inlines.Add(new LineBreak());
            wordsPara.Inlines.Add(new Bold(new Run(AmountInWords((long)Math.Round(TotalBill)) + " RUPEES ONLY")));
            doc.Blocks.Add(wordsPara);

            // ── HARDCODED WARRANTY FOOTER ─────────────────────────────────
            var warrantyPara = new Paragraph
            {
                Margin = new Thickness(0, 20, 0, 0),
                FontSize = 7.5,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 4, 0, 0)
            };
            warrantyPara.Inlines.Add(new Bold(new Run("Warranty under section 23(1)(i) of the Drug Act 1976.")));
            warrantyPara.Inlines.Add(new LineBreak());
            warrantyPara.Inlines.Add(new Run(
                "I Momina Haroon being a person resident in pakistan, carrying on business at office # 410 4th Floor " +
                "Kohistan Tower Rwp. Under the name of Master pharmaceutical Distributor do hearby give this warranty " +
                "that the drugs here-under described as sold / indented by me / specified and contained in the bill " +
                "of sale invoice, bill of landing or other documents describing the goods referred to herein do not " +
                "contravene in any way the provisions of section 23 of the Drugs Act, 1976."));
            warrantyPara.Inlines.Add(new LineBreak());
            warrantyPara.Inlines.Add(new Run("Terms:"));
            doc.Blocks.Add(warrantyPara);

            var softwarePara = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 6, 0, 0), FontSize = 7.5 };
            softwarePara.Inlines.Add(new Run("(Computer Software developed by Abuzar Consultancy. Ph 042-37426911-15)"));
            doc.Blocks.Add(softwarePara);

            return doc;
        }

        private static string AmountInWords(long n)
        {
            if (n == 0) return "ZERO";
            if (n < 0) return "MINUS " + AmountInWords(-n);

            string[] ones = { "", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
                               "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN",
                               "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
            string[] tens = { "", "", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

            string BelowThousand(long num)
            {
                if (num == 0) return "";
                if (num < 20) return ones[num] + " ";
                if (num < 100) return tens[num / 10] + (num % 10 != 0 ? " " + ones[num % 10] : "") + " ";
                return ones[num / 100] + " HUNDRED " + BelowThousand(num % 100);
            }

            var result = "";
            if (n >= 10_000_000) { result += BelowThousand(n / 10_000_000) + "CRORE "; n %= 10_000_000; }
            if (n >= 100_000)    { result += BelowThousand(n / 100_000) + "LAKH "; n %= 100_000; }
            if (n >= 1_000)      { result += BelowThousand(n / 1_000) + "THOUSAND "; n %= 1_000; }
            result += BelowThousand(n);
            return result.Trim();
        }

        // ----------------------------------------------------------------
        // Auto-select helpers called from code-behind after dialog closes
        // ----------------------------------------------------------------
        public void SelectPharmacyById(int id)
        {
            var p = _allPharmacies.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                SelectedPharmacy = p;
                PharmacySearchText = p.DisplayLabel;
            }
        }

        public void SelectDoctorById(int id)
        {
            var d = _allDoctors.FirstOrDefault(x => x.Id == id);
            if (d != null)
            {
                SelectedDoctor = d;
                DoctorSearchText = d.DisplayLabel;
            }
        }
    }
}
