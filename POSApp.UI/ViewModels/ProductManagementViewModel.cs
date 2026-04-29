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

        private Product? _selectedProduct;
        private string _productId = string.Empty;
        private string _barcode = string.Empty;
        private string _productName = string.Empty;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _wholesalePrice;
        private int _stock;
        private string? _rack;
        private Category? _selectedCategory;
        private bool _showDeletedProducts = false;

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

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set => SetProperty(ref _wholesalePrice, value);
        }

        public int Stock
        {
            get => _stock;
            set => SetProperty(ref _stock, value);
        }

        public string? Rack
        {
            get => _rack;
            set => SetProperty(ref _rack, value);
        }

        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

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

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand GenerateIdCommand { get; }

        public ProductManagementViewModel(IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;

            AddCommand = new RelayCommand(async _ => await AddProduct());
            UpdateCommand = new RelayCommand(async _ => await UpdateProduct(), _ => SelectedProduct != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteProduct(), _ => SelectedProduct != null);
            RestoreCommand = new RelayCommand(async _ => await RestoreProduct(), _ => SelectedProduct?.IsDeleted == true);
            ClearCommand = new RelayCommand(_ => ClearForm());
            RefreshCommand = new RelayCommand(async _ => await LoadData());
            GenerateIdCommand = new RelayCommand(async _ => await GenerateProductId());

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

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            var categories = await _categoryRepository.GetAllAsync();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        private void LoadProductDetails(Product product)
        {
            ProductId = product.ProductId;
            Barcode = product.Barcode;
            ProductName = product.ProductName;
            CostPrice = product.CostPrice;
            UnitPrice = product.UnitPrice;
            WholesalePrice = product.WholesalePrice;
            Stock = product.Stock;
            Rack = product.Rack;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
        }

        private async Task AddProduct()
        {
            if (string.IsNullOrWhiteSpace(ProductName))
            {
                NotificationHelper.ValidationError("Product Name");
                return;
            }

            // Auto-generate Product ID if not provided
            if (string.IsNullOrWhiteSpace(ProductId))
            {
                await GenerateProductId();
            }

            // Auto-fill barcode with Product ID if empty
            if (string.IsNullOrWhiteSpace(Barcode))
            {
                Barcode = ProductId;
            }

            try
            {
                var product = new Product
                {
                    ProductId = ProductId,
                    Barcode = Barcode,
                    ProductName = ProductName,
                    CostPrice = CostPrice,
                    UnitPrice = UnitPrice,
                    WholesalePrice = WholesalePrice,
                    Stock = Stock,
                    Rack = Rack,
                    CategoryId = SelectedCategory?.Id
                };

                await _productRepository.AddAsync(product);
                NotificationHelper.ProductAdded(ProductName);

                await LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("add product", ex.Message);
            }
        }

        private async Task UpdateProduct()
        {
            if (SelectedProduct == null) return;

            try
            {
                SelectedProduct.ProductId = ProductId;
                SelectedProduct.Barcode = string.IsNullOrWhiteSpace(Barcode) ? ProductId : Barcode;
                SelectedProduct.ProductName = ProductName;
                SelectedProduct.CostPrice = CostPrice;
                SelectedProduct.UnitPrice = UnitPrice;
                SelectedProduct.WholesalePrice = WholesalePrice;
                SelectedProduct.Stock = Stock;
                SelectedProduct.Rack = Rack;
                SelectedProduct.CategoryId = SelectedCategory?.Id;

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
            CostPrice = 0;
            UnitPrice = 0;
            WholesalePrice = 0;
            Stock = 0;
            Rack = null;
            SelectedCategory = null;
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
