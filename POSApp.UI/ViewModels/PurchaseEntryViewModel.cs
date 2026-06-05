using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.Helpers;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class PurchaseEntryViewModel : ViewModelBase
    {
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly IProductRepository _productRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ProductIdGenerator _productIdGenerator;

        // Purchase header
        private string _purchaseNumber = string.Empty;
        private DateTime _purchaseDate = DateTime.Now;
        private int? _selectedSupplierId;
        private string? _supplierName;
        private string? _notes;
        private decimal _totalAmount;

        // Existing product mode
        private Product? _selectedProduct;
        private decimal _unitCost;
        private int _quantity = 1;

        // Mode toggle
        private bool _isNewProductMode = false;

        // New product mode
        private string _newProductName = string.Empty;
        private string _newBarcode = string.Empty;
        private decimal _newCostPrice;
        private decimal _newUnitPrice;
        private decimal _newWholesalePrice;
        private decimal _newMinStock = 5;
        private string _newRack = string.Empty;
        private Category? _selectedCategory;
        private int _newQuantity = 1;

        public ObservableCollection<PurchaseItemLineViewModel> Items { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        public string PurchaseNumber
        {
            get => _purchaseNumber;
            set => SetProperty(ref _purchaseNumber, value);
        }

        public DateTime PurchaseDate
        {
            get => _purchaseDate;
            set => SetProperty(ref _purchaseDate, value);
        }

        public int? SelectedSupplierId
        {
            get => _selectedSupplierId;
            set
            {
                if (SetProperty(ref _selectedSupplierId, value) && value.HasValue)
                    SupplierName = Suppliers.FirstOrDefault(s => s.Id == value.Value)?.Name;
            }
        }

        public string? SupplierName
        {
            get => _supplierName;
            set => SetProperty(ref _supplierName, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        // --- Existing product mode ---

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    UnitCost = value.CostPrice;
                    OnPropertyChanged(nameof(CurrentStockInfo));
                    OnPropertyChanged(nameof(AvgCostPreview));
                }
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (SetProperty(ref _unitCost, value))
                    OnPropertyChanged(nameof(AvgCostPreview));
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                    OnPropertyChanged(nameof(AvgCostPreview));
            }
        }

        public string CurrentStockInfo =>
            SelectedProduct == null ? string.Empty
            : $"In stock: {SelectedProduct.Stock} units  |  Current cost: Rs.{SelectedProduct.CostPrice:N2}";

        public string AvgCostPreview
        {
            get
            {
                if (SelectedProduct == null || UnitCost <= 0) return string.Empty;
                var qty = Quantity > 0 ? Quantity : 1;
                var stock = SelectedProduct.Stock;
                var avgCost = stock > 0
                    ? ((stock * SelectedProduct.CostPrice) + (qty * UnitCost)) / (stock + qty)
                    : UnitCost;
                return $"New avg cost after purchase → Rs.{avgCost:N2}";
            }
        }

        // --- Mode toggle ---

        public bool IsNewProductMode
        {
            get => _isNewProductMode;
            set
            {
                if (SetProperty(ref _isNewProductMode, value))
                    OnPropertyChanged(nameof(IsExistingProductMode));
            }
        }

        public bool IsExistingProductMode
        {
            get => !_isNewProductMode;
            set { if (value) IsNewProductMode = false; }
        }

        // --- New product mode fields ---

        public string NewProductName
        {
            get => _newProductName;
            set => SetProperty(ref _newProductName, value);
        }

        public string NewBarcode
        {
            get => _newBarcode;
            set => SetProperty(ref _newBarcode, value);
        }

        public decimal NewCostPrice
        {
            get => _newCostPrice;
            set => SetProperty(ref _newCostPrice, value);
        }

        public decimal NewUnitPrice
        {
            get => _newUnitPrice;
            set => SetProperty(ref _newUnitPrice, value);
        }

        public decimal NewWholesalePrice
        {
            get => _newWholesalePrice;
            set => SetProperty(ref _newWholesalePrice, value);
        }

        public decimal NewMinStock
        {
            get => _newMinStock;
            set => SetProperty(ref _newMinStock, value);
        }

        public string NewRack
        {
            get => _newRack;
            set => SetProperty(ref _newRack, value);
        }

        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public int NewQuantity
        {
            get => _newQuantity;
            set => SetProperty(ref _newQuantity, value);
        }

        public ICommand AddExistingItemCommand { get; }
        public ICommand AddNewProductItemCommand { get; }
        public ICommand GenerateBarcodeCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }

        public PurchaseEntryViewModel(
            IPurchaseRepository purchaseRepository,
            IProductRepository productRepository,
            ISupplierRepository supplierRepository,
            ICategoryRepository categoryRepository)
        {
            _purchaseRepository = purchaseRepository;
            _productRepository = productRepository;
            _supplierRepository = supplierRepository;
            _categoryRepository = categoryRepository;
            _productIdGenerator = new ProductIdGenerator(productRepository);

            Items.CollectionChanged += (s, e) => CalculateTotal();

            AddExistingItemCommand = new RelayCommand(_ => AddExistingItem());
            AddNewProductItemCommand = new RelayCommand(async _ => await AddNewProductItem());
            GenerateBarcodeCommand = new RelayCommand(_ => GenerateBarcode());
            RemoveItemCommand = new RelayCommand(RemoveItem);
            SaveCommand = new RelayCommand(async _ => await SavePurchase());
            NewCommand = new RelayCommand(_ => NewPurchase());

            _ = LoadData();
        }

        private async Task LoadData()
        {
            PurchaseNumber = await _purchaseRepository.GetNextPurchaseNumberAsync();

            var suppliers = await _supplierRepository.GetActiveAsync();
            Suppliers.Clear();
            foreach (var s in suppliers) Suppliers.Add(s);

            var products = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var p in products) Products.Add(p);

            var categories = await _categoryRepository.GetAllAsync();
            Categories.Clear();
            foreach (var c in categories) Categories.Add(c);
        }

        private void GenerateBarcode()
        {
            var rand = new Random();
            NewBarcode = $"{DateTime.Now:yyMMddHHmm}{rand.Next(10, 99)}";
        }

        private void AddExistingItem()
        {
            if (SelectedProduct == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a product.");
                return;
            }
            if (Quantity <= 0) Quantity = 1;
            if (UnitCost <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Please enter a valid unit cost.");
                return;
            }

            var stock = SelectedProduct.Stock;
            var oldCost = SelectedProduct.CostPrice;
            var avgCost = stock > 0
                ? Math.Round(((stock * oldCost) + (Quantity * UnitCost)) / (stock + Quantity), 2)
                : Math.Round(UnitCost, 2);

            var existing = Items.FirstOrDefault(i => i.ProductId == SelectedProduct.ProductId && !i.IsNewProduct);
            if (existing != null)
            {
                existing.Quantity += Quantity;
            }
            else
            {
                Items.Add(new PurchaseItemLineViewModel
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.ProductName,
                    Quantity = Quantity,
                    UnitCost = UnitCost,
                    AvgCostAfter = avgCost,
                    IsNewProduct = false
                });
            }

            CalculateTotal();
            SelectedProduct = null;
            UnitCost = 0;
            Quantity = 1;
            OnPropertyChanged(nameof(CurrentStockInfo));
            OnPropertyChanged(nameof(AvgCostPreview));
        }

        private async Task AddNewProductItem()
        {
            if (string.IsNullOrWhiteSpace(NewProductName))
            {
                NotificationHelper.ValidationErrorCustom("Please enter product name.");
                return;
            }
            if (NewCostPrice <= 0 || NewUnitPrice <= 0)
            {
                NotificationHelper.ValidationErrorCustom("Please enter cost price and selling price.");
                return;
            }
            if (NewQuantity <= 0) NewQuantity = 1;
            if (string.IsNullOrWhiteSpace(NewBarcode)) GenerateBarcode();

            var existingByBarcode = await _productRepository.GetByBarcodeAsync(NewBarcode);
            if (existingByBarcode != null)
            {
                NotificationHelper.ValidationErrorCustom($"Barcode '{NewBarcode}' is already used by '{existingByBarcode.ProductName}'.");
                return;
            }

            var productId = await _productIdGenerator.GenerateProductIdAsync(SelectedCategory?.Id);

            Items.Add(new PurchaseItemLineViewModel
            {
                ProductId = productId,
                ProductName = NewProductName,
                Barcode = NewBarcode,
                Quantity = NewQuantity,
                UnitCost = NewCostPrice,
                UnitPrice = NewUnitPrice,
                WholesalePrice = NewWholesalePrice > 0 ? NewWholesalePrice : NewUnitPrice,
                MinStockThreshold = (int)NewMinStock,
                Rack = NewRack,
                CategoryId = SelectedCategory?.Id,
                AvgCostAfter = NewCostPrice,
                IsNewProduct = true
            });

            CalculateTotal();
            NewProductName = string.Empty;
            NewBarcode = string.Empty;
            NewCostPrice = 0;
            NewUnitPrice = 0;
            NewWholesalePrice = 0;
            NewMinStock = 5;
            NewRack = string.Empty;
            NewQuantity = 1;
            SelectedCategory = null;
        }

        private void RemoveItem(object? parameter)
        {
            if (parameter is PurchaseItemLineViewModel item)
            {
                Items.Remove(item);
                CalculateTotal();
            }
        }

        private void CalculateTotal()
        {
            TotalAmount = Items.Sum(i => i.Total);
        }

        private async Task SavePurchase()
        {
            if (!Items.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please add at least one item.");
                return;
            }

            try
            {
                var purchase = new PurchaseOrder
                {
                    PurchaseNumber = PurchaseNumber,
                    PurchaseDate = PurchaseDate,
                    SupplierId = SelectedSupplierId,
                    SupplierName = SupplierName,
                    TotalAmount = TotalAmount,
                    Notes = Notes
                };

                foreach (var item in Items)
                {
                    purchase.Items.Add(new PurchaseOrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitCost = item.UnitCost,
                        Total = item.Total
                    });
                }

                await _purchaseRepository.AddAsync(purchase);

                foreach (var item in Items)
                {
                    if (item.IsNewProduct)
                    {
                        var newProduct = new Product
                        {
                            ProductId = item.ProductId,
                            Barcode = item.Barcode ?? item.ProductId,
                            ProductName = item.ProductName,
                            CostPrice = item.UnitCost,
                            UnitPrice = item.UnitPrice,
                            WholesalePrice = item.WholesalePrice > 0 ? item.WholesalePrice : item.UnitPrice,
                            Stock = item.Quantity,
                            MinStockThreshold = item.MinStockThreshold,
                            Rack = item.Rack ?? string.Empty,
                            CategoryId = item.CategoryId,
                            CreatedDate = DateTime.Now
                        };
                        await _productRepository.AddAsync(newProduct);
                    }
                    else
                    {
                        var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                        if (product != null)
                        {
                            var oldStock = product.Stock;
                            var oldCost = product.CostPrice;
                            product.CostPrice = oldStock > 0
                                ? Math.Round(((oldStock * oldCost) + (item.Quantity * item.UnitCost)) / (oldStock + item.Quantity), 2)
                                : item.UnitCost;
                            product.Stock += item.Quantity;
                            await _productRepository.UpdateAsync(product);
                        }
                    }
                }

                NotificationHelper.ShowSuccess($"Purchase {PurchaseNumber} saved! Stock and average costs updated.");
                NewPurchase();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("save purchase", ex.Message);
            }
        }

        private async void NewPurchase()
        {
            Items.Clear();
            SupplierName = null;
            SelectedSupplierId = null;
            Notes = null;
            TotalAmount = 0;
            IsNewProductMode = false;
            PurchaseDate = DateTime.Now;
            PurchaseNumber = await _purchaseRepository.GetNextPurchaseNumberAsync();
            await LoadData();
        }
    }

    public sealed class PurchaseItemLineViewModel : ViewModelBase
    {
        private int _quantity;
        private decimal _unitCost;

        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public int MinStockThreshold { get; set; }
        public string? Rack { get; set; }
        public int? CategoryId { get; set; }
        public decimal AvgCostAfter { get; set; }
        public bool IsNewProduct { get; set; }

        public string TypeBadge => IsNewProduct ? "NEW" : "EXISTING";

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set
            {
                if (SetProperty(ref _unitCost, value))
                    OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => UnitCost * Quantity;
    }
}
