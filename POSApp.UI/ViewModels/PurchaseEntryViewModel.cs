using System.Collections.ObjectModel;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class PurchaseEntryViewModel : ViewModelBase
    {
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly IProductRepository _productRepository;
        private readonly ISupplierRepository _supplierRepository;

        private string _purchaseNumber = string.Empty;
        private DateTime _purchaseDate = DateTime.Now;
        private string? _supplierName;
        private int? _selectedSupplierId;
        private string? _notes;
        private string _productSearchText = string.Empty;
        private Product? _selectedProduct;
        private int _quantity = 1;
        private decimal _unitCost;
        private decimal _totalAmount;

        public ObservableCollection<PurchaseOrderItem> Items { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<Supplier> Suppliers { get; } = new();

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

        public string? SupplierName
        {
            get => _supplierName;
            set => SetProperty(ref _supplierName, value);
        }

        public int? SelectedSupplierId
        {
            get => _selectedSupplierId;
            set => SetProperty(ref _selectedSupplierId, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    _ = SearchProducts();
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
                    UnitCost = value.CostPrice;
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal UnitCost
        {
            get => _unitCost;
            set => SetProperty(ref _unitCost, value);
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        public ICommand AddItemCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand NewCommand { get; }

        public PurchaseEntryViewModel(
            IPurchaseRepository purchaseRepository,
            IProductRepository productRepository,
            ISupplierRepository supplierRepository)
        {
            _purchaseRepository = purchaseRepository;
            _productRepository = productRepository;
            _supplierRepository = supplierRepository;

            AddItemCommand = new RelayCommand(_ => AddItem());
            RemoveItemCommand = new RelayCommand(RemoveItem);
            SaveCommand = new RelayCommand(async _ => await SavePurchase());
            NewCommand = new RelayCommand(_ => NewPurchase());

            Items.CollectionChanged += (s, e) => CalculateTotal();

            _ = LoadData();
        }

        private async Task LoadData()
        {
            PurchaseNumber = await _purchaseRepository.GetNextPurchaseNumberAsync();

            var suppliers = await _supplierRepository.GetActiveAsync();
            Suppliers.Clear();
            foreach (var supplier in suppliers)
            {
                Suppliers.Add(supplier);
            }

            var products = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }
        }

        private async Task SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                await LoadData();
                return;
            }

            var products = await _productRepository.SearchAsync(ProductSearchText);
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }
        }

        private void AddItem()
        {
            if (SelectedProduct == null)
            {
                NotificationHelper.ValidationErrorCustom("Please select a product.");
                return;
            }

            if (Quantity <= 0)
            {
                Quantity = 1;
            }

            var item = new PurchaseOrderItem
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.ProductName,
                Quantity = Quantity,
                UnitCost = UnitCost,
                Total = UnitCost * Quantity
            };

            Items.Add(item);
            CalculateTotal();

            SelectedProduct = null;
            Quantity = 1;
            UnitCost = 0;
            ProductSearchText = string.Empty;
        }

        private void RemoveItem(object? parameter)
        {
            if (parameter is PurchaseOrderItem item)
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
                    purchase.Items.Add(item);
                }

                await _purchaseRepository.AddAsync(purchase);

                // Update product stock levels
                foreach (var item in Items)
                {
                    var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.Quantity;
                        product.CostPrice = item.UnitCost; // Update cost price
                        await _productRepository.UpdateAsync(product);
                    }
                }

                NotificationHelper.ShowSuccess($"Purchase {PurchaseNumber} saved successfully! Stock updated.");
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
            PurchaseNumber = await _purchaseRepository.GetNextPurchaseNumberAsync();
            PurchaseDate = DateTime.Now;
        }
    }
}
