using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public sealed class PurchaseReturnViewModel : ViewModelBase
    {
        private readonly IPurchaseRepository _purchaseRepository;
        private readonly IProductRepository _productRepository;

        private string _searchPurchaseNumber = string.Empty;
        private PurchaseOrder? _originalPurchase;
        private string _returnNumber = string.Empty;
        private DateTime _returnDate = DateTime.Now;
        private string _returnReason = string.Empty;
        private decimal _totalReturnAmount;

        public ObservableCollection<PurchaseReturnItemViewModel> ReturnItems { get; } = new();

        public string SearchPurchaseNumber
        {
            get => _searchPurchaseNumber;
            set => SetProperty(ref _searchPurchaseNumber, value);
        }

        public PurchaseOrder? OriginalPurchase
        {
            get => _originalPurchase;
            set
            {
                if (SetProperty(ref _originalPurchase, value) && value != null)
                    LoadReturnItems(value);
            }
        }

        public string ReturnNumber
        {
            get => _returnNumber;
            set => SetProperty(ref _returnNumber, value);
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

        public PurchaseReturnViewModel(IPurchaseRepository purchaseRepository, IProductRepository productRepository)
        {
            _purchaseRepository = purchaseRepository;
            _productRepository = productRepository;

            ReturnItems.CollectionChanged += ReturnItems_CollectionChanged;

            SearchCommand = new RelayCommand(async _ => await SearchPurchase());
            ProcessReturnCommand = new RelayCommand(async _ => await ProcessReturn());
            CancelCommand = new RelayCommand(_ => { });

            _ = GenerateReturnNumber();
        }

        private void ReturnItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (PurchaseReturnItemViewModel item in e.OldItems)
                    item.PropertyChanged -= ReturnItem_PropertyChanged;
            if (e.NewItems != null)
                foreach (PurchaseReturnItemViewModel item in e.NewItems)
                    item.PropertyChanged += ReturnItem_PropertyChanged;
            RecalculateTotal();
        }

        private void ReturnItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PurchaseReturnItemViewModel.Total))
                RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalReturnAmount = ReturnItems.Sum(i => i.Total);
        }

        private async Task GenerateReturnNumber()
        {
            var next = await _purchaseRepository.GetNextPurchaseNumberAsync();
            ReturnNumber = "PR-" + next.Replace("PUR-", "");
        }

        private async Task SearchPurchase()
        {
            if (string.IsNullOrWhiteSpace(SearchPurchaseNumber))
            {
                NotificationHelper.ValidationErrorCustom("Please enter a purchase number to search.");
                return;
            }

            try
            {
                var purchase = await _purchaseRepository.GetByPurchaseNumberAsync(SearchPurchaseNumber);

                if (purchase == null)
                {
                    NotificationHelper.ShowWarning($"Purchase '{SearchPurchaseNumber}' was not found.", "Not Found");
                    return;
                }

                if (purchase.Notes?.StartsWith("RETURN:") == true)
                {
                    NotificationHelper.ShowWarning("This is already a return record.", "Invalid");
                    return;
                }

                OriginalPurchase = purchase;
                NotificationHelper.ShowInfo(
                    $"Purchase found!\n\nSupplier: {purchase.SupplierName ?? "N/A"}\nTotal: Rs.{purchase.TotalAmount:N2}",
                    "Purchase Found");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("search purchase", ex.Message);
            }
        }

        private void LoadReturnItems(PurchaseOrder purchase)
        {
            ReturnItems.Clear();
            foreach (var item in purchase.Items)
            {
                ReturnItems.Add(new PurchaseReturnItemViewModel
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    OriginalQuantity = (int)item.Quantity,
                    ReturnQuantity = 0,
                    UnitCost = item.UnitCost,
                    Total = 0
                });
            }
        }

        private async Task ProcessReturn()
        {
            if (OriginalPurchase == null)
            {
                NotificationHelper.ValidationErrorCustom("Please search and select a purchase first.");
                return;
            }

            var itemsToReturn = ReturnItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (!itemsToReturn.Any())
            {
                NotificationHelper.ValidationErrorCustom("Please specify quantities to return for at least one item.");
                return;
            }

            foreach (var item in itemsToReturn)
            {
                if (item.ReturnQuantity > item.OriginalQuantity)
                {
                    NotificationHelper.ValidationErrorCustom($"Return qty for '{item.ProductName}' cannot exceed original qty of {item.OriginalQuantity}.");
                    return;
                }
            }

            try
            {
                var noteparts = new List<string> { $"RETURN: for {OriginalPurchase.PurchaseNumber}" };
                if (!string.IsNullOrWhiteSpace(ReturnReason))
                    noteparts.Add($"Reason: {ReturnReason}");

                var returnOrder = new PurchaseOrder
                {
                    PurchaseNumber = ReturnNumber,
                    PurchaseDate = ReturnDate,
                    SupplierId = OriginalPurchase.SupplierId,
                    SupplierName = OriginalPurchase.SupplierName,
                    TotalAmount = -TotalReturnAmount,
                    Notes = string.Join(". ", noteparts)
                };

                foreach (var item in itemsToReturn)
                {
                    returnOrder.Items.Add(new PurchaseOrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = -item.ReturnQuantity,
                        UnitCost = item.UnitCost,
                        Total = -item.Total
                    });

                    // Decrease stock — items returned to supplier
                    var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock = Math.Max(0, product.Stock - item.ReturnQuantity);
                        await _productRepository.UpdateAsync(product);
                    }
                }

                await _purchaseRepository.AddAsync(returnOrder);

                NotificationHelper.ShowSuccess($"Purchase return {ReturnNumber} processed! Stock updated.");

                await GenerateReturnNumber();
                OriginalPurchase = null;
                ReturnItems.Clear();
                SearchPurchaseNumber = string.Empty;
                ReturnReason = string.Empty;
                TotalReturnAmount = 0;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("process purchase return", ex.Message);
            }
        }
    }

    public sealed class PurchaseReturnItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private int _originalQuantity;
        private int _returnQuantity;
        private decimal _unitCost;
        private decimal _total;

        public string ProductId { get => _productId; set => SetProperty(ref _productId, value); }
        public string ProductName { get => _productName; set => SetProperty(ref _productName, value); }
        public int OriginalQuantity { get => _originalQuantity; set => SetProperty(ref _originalQuantity, value); }

        public int ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                if (SetProperty(ref _returnQuantity, value))
                    Total = value * UnitCost;
            }
        }

        public decimal UnitCost { get => _unitCost; set => SetProperty(ref _unitCost, value); }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }
    }
}
