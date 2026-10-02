using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.Infrastructure.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class ProductManagementViewModel : ViewModelBase
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFavoriteRepository? _favoriteRepository;
        private readonly ITaxRepository? _taxRepository;
        private IReadOnlySet<int> _quickKeyIds = new HashSet<int>();
        private TaxCategoryChoice? _selectedTaxCategory;

        /// <summary>A product's tax-category choice; Id null means "the shop's default".</summary>
        public sealed record TaxCategoryChoice(int? Id, string Label);

        /// <summary>The US tax categories to pick from, headed by "Default (…)". Empty when there are none.</summary>
        public ObservableCollection<TaxCategoryChoice> TaxCategoryChoices { get; } = new();

        public bool HasTaxCategories => TaxCategoryChoices.Count > 0;

        public TaxCategoryChoice? SelectedTaxCategory
        {
            get => _selectedTaxCategory;
            set => SetProperty(ref _selectedTaxCategory, value);
        }

        private Product? _selectedProduct;
        private string _productId = string.Empty;
        private string _barcode = string.Empty;
        private string _productName = string.Empty;
        private decimal? _costPrice;
        private decimal? _unitPrice;
        private decimal? _wholesalePrice;
        private int? _stock;
        private int? _minStockThreshold;
        private string? _rack;
        private string? _batchNo;
        private DateTime? _expiryDate;
        private Category? _selectedCategory;
        private bool _showDeletedProducts = false;
        private string _searchText = string.Empty;

        private readonly List<Product> _allProducts = new();

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Category> Categories { get; } = new();

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    LoadProductDetails(value);
                }
            }
        }

        public string ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        public decimal? CostPrice
        {
            get => _costPrice;
            set
            {
                if (SetProperty(ref _costPrice, value) && value.HasValue && value.Value > 0 && IsPharmacyUser)
                    UnitPrice = Math.Round(value.Value * 0.85m, 2);
            }
        }

        public decimal? UnitPrice
        {
            get => _unitPrice;
            set => SetProperty(ref _unitPrice, value);
        }

        public decimal? WholesalePrice
        {
            get => _wholesalePrice;
            set => SetProperty(ref _wholesalePrice, value);
        }

        public int? Stock
        {
            get => _stock;
            set => SetProperty(ref _stock, value);
        }

        public int? MinStockThreshold
        {
            get => _minStockThreshold;
            set => SetProperty(ref _minStockThreshold, value);
        }

        public string? Rack
        {
            get => _rack;
            set => SetProperty(ref _rack, value);
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

        public bool IsPharmacyUser { get; } =
            SessionManager.HasPermission(POSApp.Core.Entities.Permissions.PharmacySale);

        /// <summary>Batch and expiry: the distributor pharmacy role, and every US shop.</summary>
        public Visibility PharmacyFieldsVisibility =>
            SessionManager.HasPermission(POSApp.Core.Entities.Permissions.PharmacyManage) || Region.IsUnitedStates
                ? Visibility.Visible
                : Visibility.Collapsed;

        // ── US front store ────────────────────────────────────────────────────
        private int _minimumAge;
        private bool _isFsaEligible;
        private bool _isPse;
        private decimal? _pseBaseMgPerPack;

        /// <summary>ID check (US, every edition).</summary>
        public bool ShowFrontStoreFields => Region.IsUnitedStates;

        /// <summary>FSA/HSA and PSE (US, the Pro front-store feature).</summary>
        public bool ShowPharmacyFields => Region.IsUnitedStates && EditionGate.IsEnabled(POSApp.Core.Services.AppFeature.FrontStorePharmacy);

        /// <summary>0, 18 or 21 as the combo's index 0, 1, 2.</summary>
        public int MinimumAgeIndex
        {
            get => _minimumAge >= 21 ? 2 : _minimumAge >= 18 ? 1 : 0;
            set
            {
                var age = value == 2 ? 21 : value == 1 ? 18 : 0;
                if (age == _minimumAge) return;
                _minimumAge = age;
                OnPropertyChanged();
            }
        }

        public bool IsFsaEligible
        {
            get => _isFsaEligible;
            set => SetProperty(ref _isFsaEligible, value);
        }

        public bool IsPse
        {
            get => _isPse;
            set => SetProperty(ref _isPse, value);
        }

        public decimal? PseBaseMgPerPack
        {
            get => _pseBaseMgPerPack;
            set => SetProperty(ref _pseBaseMgPerPack, value);
        }

        private void LoadFrontStoreFields(Product? product)
        {
            _minimumAge = product?.MinimumAge ?? 0;
            OnPropertyChanged(nameof(MinimumAgeIndex));
            IsFsaEligible = product?.IsFsaEligible ?? false;
            IsPse = product?.IsPse ?? false;
            PseBaseMgPerPack = product is { PseBaseMgPerPack: > 0 } ? product.PseBaseMgPerPack : null;
        }

        private void ApplyFrontStoreFields(Product product)
        {
            if (!ShowFrontStoreFields) return;
            product.MinimumAge = _minimumAge;
            if (!ShowPharmacyFields) return;
            product.IsFsaEligible = IsFsaEligible;
            product.IsPse = IsPse;
            product.PseBaseMgPerPack = IsPse ? PseBaseMgPerPack ?? 0 : 0;
        }

        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public Action? OnProductAdded { get; set; }

        public bool ShowDeletedProducts
        {
            get => _showDeletedProducts;
            set
            {
                if (SetProperty(ref _showDeletedProducts, value))
                {
                    _ = LoadData(); // Reload data when toggle changes
                }
            }
        }

        /// <summary>
        /// Free-text search across every column shown in the product list
        /// (barcode, name, category, prices, stock, rack, batch, expiry).
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand GenerateIdCommand { get; }
        public ICommand GenerateBarcodeCommand { get; }
        public ICommand AutoRetailPriceCommand { get; }
        public ICommand ToggleQuickKeyCommand { get; }

        /// <summary>
        /// Ids of the products on the sale screens' quick keys (☆ column).
        /// Replaced, never mutated, so the grid re-reads every star.
        /// </summary>
        public IReadOnlySet<int> QuickKeyIds
        {
            get => _quickKeyIds;
            private set => SetProperty(ref _quickKeyIds, value);
        }

        public ProductManagementViewModel(IProductRepository productRepository, ICategoryRepository categoryRepository,
                                          IFavoriteRepository? favoriteRepository = null, ITaxRepository? taxRepository = null)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _favoriteRepository = favoriteRepository;
            _taxRepository = taxRepository;

            AddCommand = new RelayCommand(async _ => await AddProduct());
            UpdateCommand = new RelayCommand(async _ => await UpdateProduct(), _ => SelectedProduct != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteProduct(), _ => SelectedProduct != null);
            RestoreCommand = new RelayCommand(async _ => await RestoreProduct(), _ => SelectedProduct?.IsDeleted == true);
            ClearCommand = new RelayCommand(_ => ClearForm());
            RefreshCommand = new RelayCommand(async _ => await LoadData());
            GenerateIdCommand = new RelayCommand(async _ => await GenerateProductId());
            GenerateBarcodeCommand = new RelayCommand(_ => GenerateBarcode());
            ToggleQuickKeyCommand = new RelayCommand(async p => await ToggleQuickKeyAsync(p as Product));
            AutoRetailPriceCommand = new RelayCommand(_ =>
            {
                if (CostPrice.HasValue && CostPrice.Value > 0)
                    UnitPrice = Math.Round(CostPrice.Value * 0.85m, 2);
                else
                    NotificationHelper.ValidationErrorCustom("Enter Cost Price first to auto-calculate Retail Price.");
            });

            _ = LoadData();
        }

        private async Task LoadData()
        {
            IEnumerable<Product> products;

            if (ShowDeletedProducts)
            {
                // Include deleted products
                products = await _productRepository.GetAllIncludingDeletedAsync();
            }
            else
            {
                // Only active products
                products = await _productRepository.GetAllAsync();
            }

            _allProducts.Clear();
            _allProducts.AddRange(products);
            ApplyFilter();

            var categories = await _categoryRepository.GetAllAsync();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            if (_favoriteRepository != null)
                QuickKeyIds = await _favoriteRepository.GetQuickKeyProductIdsAsync();

            await LoadTaxCategoriesAsync();
        }

        private async Task LoadTaxCategoriesAsync()
        {
            if (_taxRepository == null || !Region.IsUnitedStates) return;
            try
            {
                var settings = await _taxRepository.GetSettingsAsync();
                TaxCategoryChoices.Clear();
                if (settings.Categories.Count > 0)
                {
                    var fallback = settings.Default;
                    TaxCategoryChoices.Add(new TaxCategoryChoice(null, $"Default ({fallback?.Name})"));
                    foreach (var c in settings.Categories)
                        TaxCategoryChoices.Add(new TaxCategoryChoice(c.Id, $"{c.Name} ({c.RatePercent:0.###}%)"));
                }
                OnPropertyChanged(nameof(HasTaxCategories));
                SelectedTaxCategory = ChoiceFor(SelectedProduct?.TaxCategoryId);
            }
            catch
            {
                // Tax categories are optional on this screen.
            }
        }

        private TaxCategoryChoice? ChoiceFor(int? taxCategoryId) =>
            TaxCategoryChoices.FirstOrDefault(c => c.Id == taxCategoryId) ?? TaxCategoryChoices.FirstOrDefault();

        /// <summary>Stars or un-stars a product as a quick key on the sale screens.</summary>
        public async Task ToggleQuickKeyAsync(Product? product)
        {
            if (product == null || _favoriteRepository == null) return;

            var add = !QuickKeyIds.Contains(product.Id);
            if (add && product.IsDeleted)
            {
                NotificationHelper.ValidationErrorCustom("Restore this product before adding it to quick keys.");
                return;
            }

            try
            {
                await _favoriteRepository.SetQuickKeyAsync(product.Id, SessionManager.CurrentUser?.Id ?? 0, add);
                QuickKeyIds = await _favoriteRepository.GetQuickKeyProductIdsAsync();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update quick keys", ex.Message);
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<Product> filtered = _allProducts;

            var term = _searchText?.Trim();
            if (!string.IsNullOrWhiteSpace(term))
            {
                filtered = _allProducts.Where(p => ProductMatches(p, term));
            }

            Products.Clear();
            foreach (var product in filtered)
                Products.Add(product);
        }

        /// <summary>
        /// Matches the search term against every field displayed in the product list.
        /// </summary>
        private static bool ProductMatches(Product p, string term)
        {
            bool Contains(string? value) =>
                value != null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

            return Contains(p.Barcode)
                || Contains(p.ProductName)
                || Contains(p.Category?.Name)
                || Contains(p.Rack)
                || Contains(p.BatchNo)
                || Contains(p.CostPrice.ToString("0.##"))
                || Contains(p.UnitPrice.ToString("0.##"))
                || Contains(p.WholesalePrice.ToString("0.##"))
                || Contains(p.Stock.ToString())
                || Contains(p.ExpiryDate?.ToString("MM/yyyy"));
        }

        private void LoadProductDetails(Product product)
        {
            ProductId = product.ProductId;
            Barcode = product.Barcode;
            ProductName = product.ProductName;
            CostPrice = product.CostPrice;
            UnitPrice = product.UnitPrice;
            WholesalePrice = product.WholesalePrice;
            Stock = product.Stock == 0 ? (int?)null : product.Stock;
            MinStockThreshold = product.MinStockThreshold == 0 ? (int?)null : product.MinStockThreshold;
            Rack = product.Rack;
            BatchNo = product.BatchNo;
            ExpiryDate = product.ExpiryDate;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
            SelectedTaxCategory = ChoiceFor(product.TaxCategoryId);
            LoadFrontStoreFields(product);
        }

        private async Task AddProduct()
        {
            if (string.IsNullOrWhiteSpace(ProductName))
            {
                NotificationHelper.ValidationError("Product Name");
                return;
            }

            // Sale price must not represent a loss. Equality (zero margin) is allowed.
            if ((UnitPrice ?? 0) < (CostPrice ?? 0))
            {
                NotificationHelper.ValidationErrorCustom("Sale price cannot be lower than purchase price.");
                return;
            }

            // Duplicate barcode guard — if barcode already exists, switch to edit mode instead
            if (!string.IsNullOrWhiteSpace(Barcode))
            {
                var dup = await _productRepository.GetByBarcodeAsync(Barcode.Trim());
                if (dup != null)
                {
                    NotificationHelper.ShowInfo(
                        "A product with this barcode already exists. We've loaded it for editing instead.");
                    var inList = Products.FirstOrDefault(p => p.Id == dup.Id) ?? dup;
                    SelectedProduct = inList;
                    return;
                }
            }

            // Always auto-generate the internal Product ID
            await GenerateProductId();

            // Use barcode as-is; if not provided, fall back to the generated Product ID
            if (string.IsNullOrWhiteSpace(Barcode))
                Barcode = ProductId;

            try
            {
                var product = new Product
                {
                    ProductId = ProductId,
                    Barcode = Barcode,
                    ProductName = ProductName,
                    CostPrice = CostPrice ?? 0,
                    UnitPrice = UnitPrice ?? 0,
                    WholesalePrice = WholesalePrice ?? 0,
                    Stock = Stock ?? 0,
                    MinStockThreshold = MinStockThreshold ?? 0,
                    Rack = Rack,
                    BatchNo = string.IsNullOrWhiteSpace(BatchNo) ? null : BatchNo.Trim(),
                    ExpiryDate = ExpiryDate,
                    CategoryId = SelectedCategory?.Id,
                    TaxCategoryId = SelectedTaxCategory?.Id
                };
                ApplyFrontStoreFields(product);

                await _productRepository.AddAsync(product);
                NotificationHelper.ProductAdded(ProductName);

                await LoadData();
                ClearForm();
                OnProductAdded?.Invoke();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("add product", ex.Message);
            }
        }

        private async Task UpdateProduct()
        {
            if (SelectedProduct == null) return;

            // Sale price must not represent a loss. Equality (zero margin) is allowed.
            if ((UnitPrice ?? 0) < (CostPrice ?? 0))
            {
                NotificationHelper.ValidationErrorCustom("Sale price cannot be lower than purchase price.");
                return;
            }

            try
            {
                // ProductId is immutable once created — only barcode is user-editable
                SelectedProduct.Barcode = string.IsNullOrWhiteSpace(Barcode) ? SelectedProduct.ProductId : Barcode;
                SelectedProduct.ProductName = ProductName;
                SelectedProduct.CostPrice = CostPrice ?? 0;
                SelectedProduct.UnitPrice = UnitPrice ?? 0;
                SelectedProduct.WholesalePrice = WholesalePrice ?? 0;
                SelectedProduct.Stock = Stock ?? 0;
                SelectedProduct.MinStockThreshold = MinStockThreshold ?? 0;
                SelectedProduct.Rack = Rack;
                SelectedProduct.BatchNo = string.IsNullOrWhiteSpace(BatchNo) ? null : BatchNo.Trim();
                SelectedProduct.ExpiryDate = ExpiryDate;
                SelectedProduct.CategoryId = SelectedCategory?.Id;
                if (HasTaxCategories)
                    SelectedProduct.TaxCategoryId = SelectedTaxCategory?.Id;
                ApplyFrontStoreFields(SelectedProduct);

                await _productRepository.UpdateAsync(SelectedProduct);
                NotificationHelper.ProductUpdated(ProductName);

                await LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update product", ex.Message);
            }
        }

        private async Task DeleteProduct()
        {
            if (SelectedProduct == null) return;

            if (NotificationHelper.ConfirmDelete(SelectedProduct.ProductName, "product"))
            {
                try
                {
                    // Soft delete - repository handles setting IsDeleted flag
                    await _productRepository.DeleteAsync(SelectedProduct.Id);
                    NotificationHelper.ProductDeleted(SelectedProduct.ProductName);

                    await LoadData();
                    ClearForm();
                }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("delete product", ex.Message);
                }
            }
        }

        private async Task RestoreProduct()
        {
            if (SelectedProduct == null || !SelectedProduct.IsDeleted) return;

            try
            {
                SelectedProduct.IsDeleted = false;
                SelectedProduct.ModifiedDate = DateTime.Now;

                await _productRepository.UpdateAsync(SelectedProduct);
                NotificationHelper.ShowSuccess($"Product '{SelectedProduct.ProductName}' has been restored.");

                await LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("restore product", ex.Message);
            }
        }

        private void ClearForm()
        {
            SelectedProduct = null;
            ProductId = string.Empty;
            Barcode = string.Empty;
            ProductName = string.Empty;
            CostPrice = null;
            UnitPrice = null;
            WholesalePrice = null;
            Stock = null;
            MinStockThreshold = null;
            Rack = null;
            BatchNo = null;
            ExpiryDate = null;
            SelectedCategory = null;
            SelectedTaxCategory = ChoiceFor(null);
            LoadFrontStoreFields(null);
        }

        private void GenerateBarcode()
        {
            // 12-digit timestamp barcode: yyMMddHHmmss — unique per second, numeric, readable
            Barcode = DateTime.Now.ToString("yyMMddHHmmss");
        }

        public async Task ValidateBarcodeAsync()
        {
            var trimmed = Barcode?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) return;

            // Skip check when editing and the barcode hasn't changed
            if (SelectedProduct != null && SelectedProduct.Barcode == trimmed) return;

            var existing = await _productRepository.GetByBarcodeAsync(trimmed);
            if (existing == null) return;

            NotificationHelper.ShowInfo(
                "A product with this barcode already exists. We've loaded it for editing instead.");

            // Find the in-list instance so the DataGrid row highlights correctly
            var inList = Products.FirstOrDefault(p => p.Id == existing.Id) ?? existing;
            SelectedProduct = inList;
        }

        private async Task GenerateProductId()
        {
            try
            {
                var generator = new ProductIdGenerator(_productRepository);
                ProductId = await generator.GenerateProductIdAsync(SelectedCategory?.Id);

                // Auto-fill barcode with generated ID if barcode is empty
                if (string.IsNullOrWhiteSpace(Barcode))
                {
                    Barcode = ProductId;
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("generate Product ID", ex.Message);
            }
        }
    }
}
