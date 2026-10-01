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
using POSApp.Core.Services;

namespace POSApp.UI.ViewModels
{
    public sealed class SaleReturnViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;

        private string _searchInvoiceNumber = string.Empty;
        private Sale? _originalSale;
        private string _returnInvoiceNumber = string.Empty;
        private DateTime _returnDate = AppClock.Now;
        private bool _returnDateChosenByUser;
        private string _returnReason = string.Empty;
        private decimal _totalReturnAmount;
        private string _searchStatus = string.Empty;

        public ObservableCollection<ReturnItemViewModel> ReturnItems { get; } = new();

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
                    LoadReturnItems(value);
            }
        }

        public string ReturnInvoiceNumber
        {
            get => _returnInvoiceNumber;
            set => SetProperty(ref _returnInvoiceNumber, value);
        }

        /// <summary>The date shown on screen; setting it marks it as the user's own choice (see <see cref="SaleTime"/>).</summary>
        public DateTime ReturnDate
        {
            get => _returnDate;
            set
            {
                if (SetProperty(ref _returnDate, value))
                    _returnDateChosenByUser = true;
            }
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

        public string SearchStatus
        {
            get => _searchStatus;
            set => SetProperty(ref _searchStatus, value);
        }

        public ICommand SearchCommand { get; }
        public ICommand ProcessAndPrintCommand { get; }
        public ICommand CancelCommand { get; }

        public SaleReturnViewModel(ISaleRepository saleRepository, IProductRepository productRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;

            ReturnItems.CollectionChanged += ReturnItems_CollectionChanged;

            SearchCommand = new RelayCommand(async _ => await SearchInvoice());
            ProcessAndPrintCommand = new RelayCommand(async _ => await ProcessAndPrint());
            CancelCommand = new RelayCommand(_ => { });

            _ = GenerateReturnInvoiceNumber();
        }

        private void ReturnItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (ReturnItemViewModel item in e.OldItems)
                    item.PropertyChanged -= ReturnItem_PropertyChanged;

            if (e.NewItems != null)
                foreach (ReturnItemViewModel item in e.NewItems)
                    item.PropertyChanged += ReturnItem_PropertyChanged;

            RecalculateTotal();
        }

        private void ReturnItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReturnItemViewModel.Total))
                RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalReturnAmount = ReturnItems.Sum(i => i.Total);
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

            SearchStatus = string.Empty;

            try
            {
                var sale = await _saleRepository.GetByInvoiceNumberAsync(SearchInvoiceNumber);

                if (sale == null)
                {
                    SearchStatus = $"Invoice '{SearchInvoiceNumber}' not found.";
                    ReturnItems.Clear();
                    return;
                }

                if (sale.SaleType == "Return")
                {
                    SearchStatus = "This invoice is already a return — cannot process again.";
                    ReturnItems.Clear();
                    return;
                }

                SearchStatus = $"✓ {sale.CustomerName}  |  Original: {Region.Money(sale.TotalBill)}  |  Date: {Region.Date(sale.SaleDate, DateFormat.Document)}";
                OriginalSale = sale;
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
                    OriginalQuantity = (int)item.Quantity,
                    LineQuantity = item.Quantity,
                    ReturnQuantity = 0,
                    UnitPrice = item.UnitPrice,
                    CostPrice = item.CostPrice,
                    DiscountType = item.DiscountType,
                    DiscountPercent = item.DiscountPercent,
                    Total = 0
                });
            }
        }

        private async Task ProcessAndPrint()
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

            foreach (var item in itemsToReturn)
            {
                if (item.ReturnQuantity > item.OriginalQuantity)
                {
                    NotificationHelper.ValidationErrorCustom($"Return quantity for '{item.ProductName}' cannot exceed original quantity of {item.OriginalQuantity}.");
                    return;
                }
            }

            try
            {
                // Fresh return number and the real return time (the screen may have been open for hours).
                await GenerateReturnInvoiceNumber();
                _returnDate = SaleTime.Resolve(_returnDate, _returnDateChosenByUser, AppClock.Now);
                OnPropertyChanged(nameof(ReturnDate));

                var noteparts = new List<string> { $"Return for Invoice: {OriginalSale.InvoiceNumber}" };
                if (!string.IsNullOrWhiteSpace(ReturnReason))
                    noteparts.Add($"Reason: {ReturnReason}");

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
                    BillNote = string.Join(". ", noteparts),
                    TotalBill = -TotalReturnAmount,
                    ReceiveCash = -TotalReturnAmount,
                    Balance = 0
                };

                foreach (var item in itemsToReturn)
                {
                    returnSale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = -item.ReturnQuantity,
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        DiscountPercent = item.DiscountPercent,
                        DiscountType = item.DiscountType,
                        Total = -item.Total
                    });

                    var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.ReturnQuantity;
                        await _productRepository.UpdateAsync(product);
                    }
                }

                await _saleRepository.AddAsync(returnSale);

                // Print silently, then reset — no popup
                PrintReturnReceipt();

                await GenerateReturnInvoiceNumber();
                _returnDate = AppClock.Now;
                _returnDateChosenByUser = false;
                OnPropertyChanged(nameof(ReturnDate));
                OriginalSale = null;
                ReturnItems.Clear();
                SearchInvoiceNumber = string.Empty;
                ReturnReason = string.Empty;
                TotalReturnAmount = 0;
                SearchStatus = string.Empty;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("process return", ex.Message);
            }
        }

        private void PrintReturnReceipt()
        {
            try
            {
                var printDoc = CreateReturnReceipt();
                var printDialog = new PrintDialog();

                printDoc.PageWidth = 280;
                printDoc.PageHeight = double.NaN;
                printDoc.ColumnWidth = 260;
                printDoc.PagePadding = new System.Windows.Thickness(5);
                printDoc.FontSize = 10;

                printDialog.PrintDocument(
                    ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                    "Return Receipt");
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print return receipt", ex.Message);
            }
        }

        internal FlowDocument CreateReturnReceipt()
        {
            var doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.TextAlignment = System.Windows.TextAlignment.Left;

            // --- HEADER (per-client branding, see Admin -> Receipt Settings) ---
            doc.Blocks.Add(ReceiptBranding.BuildHeader(nameFontSize: 24, lineFontSize: 14));

            // --- TITLE ---
            var titlePara = new Paragraph(new Bold(new Run("Return Receipt")))
            {
                TextAlignment = System.Windows.TextAlignment.Center,
                FontSize = 16,
                BorderBrush = Brushes.Black,
                BorderThickness = new System.Windows.Thickness(1),
                Padding = new System.Windows.Thickness(3),
                Margin = new System.Windows.Thickness(0, 2, 0, 2)
            };
            doc.Blocks.Add(titlePara);

            // --- METADATA TABLE ---
            var metaTable = new Table { CellSpacing = 0 };
            metaTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            var metaGroup = new TableRowGroup();

            TableCell MetaCell(string text, int columnSpan = 1)
            {
                var cell = new TableCell(new Paragraph(new Run(text)) { Margin = new System.Windows.Thickness(0) })
                {
                    Padding = new System.Windows.Thickness(0, 1, 0, 1)
                };
                if (columnSpan > 1) cell.ColumnSpan = columnSpan;
                return cell;
            }

            var row1 = new TableRow();
            row1.Cells.Add(MetaCell($"Return No: {ReturnInvoiceNumber}"));
            row1.Cells.Add(MetaCell($"Date: {Region.DocumentDateTime(ReturnDate)}"));
            metaGroup.Rows.Add(row1);

            var row2 = new TableRow();
            row2.Cells.Add(MetaCell($"Orig Invoice: {OriginalSale?.InvoiceNumber}"));
            row2.Cells.Add(MetaCell($"Customer: {OriginalSale?.CustomerName}"));
            metaGroup.Rows.Add(row2);

            if (!string.IsNullOrWhiteSpace(ReturnReason))
            {
                var noteRow = new TableRow();
                noteRow.Cells.Add(MetaCell($"Reason: {ReturnReason}", columnSpan: 2));
                metaGroup.Rows.Add(noteRow);
            }

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            doc.Blocks.Add(new Paragraph(new Run("---------------------------------------------")) { Margin = new System.Windows.Thickness(0, 1, 0, 1) });

            // --- ITEMS TABLE ---
            var itemsTable = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new System.Windows.Thickness(0, 1, 0, 1) };
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(0.4, System.Windows.GridUnitType.Star) }); // S.No
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(2.1, System.Windows.GridUnitType.Star) }); // Product Name
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(0.6, System.Windows.GridUnitType.Star) }); // Qty
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(0.9, System.Windows.GridUnitType.Star) }); // Price
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(0.7, System.Windows.GridUnitType.Star) }); // Disc
            itemsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(1.1, System.Windows.GridUnitType.Star) }); // Total

            var itemsGroup = new TableRowGroup();

            TableCell ItemCell(string text, System.Windows.TextAlignment align)
            {
                return new TableCell(new Paragraph(new Run(text)) { TextAlignment = align, Margin = new System.Windows.Thickness(0) })
                {
                    BorderThickness = new System.Windows.Thickness(0.5),
                    BorderBrush = Brushes.Black,
                    Padding = new System.Windows.Thickness(2, 1, 2, 1)
                };
            }

            var headerRow = new TableRow { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            headerRow.Cells.Add(ItemCell("S.No", System.Windows.TextAlignment.Center));
            headerRow.Cells.Add(ItemCell("Product Name", System.Windows.TextAlignment.Left));
            headerRow.Cells.Add(ItemCell("Qty", System.Windows.TextAlignment.Center));
            headerRow.Cells.Add(ItemCell("Price", System.Windows.TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Disc", System.Windows.TextAlignment.Right));
            headerRow.Cells.Add(ItemCell("Total", System.Windows.TextAlignment.Center));
            itemsGroup.Rows.Add(headerRow);

            int serial = 1;
            foreach (var item in ReturnItems.Where(i => i.ReturnQuantity > 0))
            {
                var row = new TableRow();
                row.Cells.Add(ItemCell(serial++.ToString(), System.Windows.TextAlignment.Center));
                row.Cells.Add(ItemCell(item.ProductName, System.Windows.TextAlignment.Left));
                row.Cells.Add(ItemCell(item.ReturnQuantity.ToString(), System.Windows.TextAlignment.Center));
                row.Cells.Add(ItemCell(Region.Compact(item.UnitPrice), System.Windows.TextAlignment.Right));
                row.Cells.Add(ItemCell(item.DiscountPercent.ToString("N0"), System.Windows.TextAlignment.Right));
                row.Cells.Add(ItemCell(Region.Number(item.Total), System.Windows.TextAlignment.Right));
                itemsGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // --- TOTALS ---
            var totalsTable = new Table { CellSpacing = 0 };
            totalsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(2, System.Windows.GridUnitType.Star) });
            totalsTable.Columns.Add(new TableColumn { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            var totalsGroup = new TableRowGroup();

            void AddTotalRow(string label, string value, bool bold = false, double fontSize = 10)
            {
                var row = new TableRow();
                var labelPara = new Paragraph(new Run(label)) { FontSize = fontSize, Margin = new System.Windows.Thickness(0) };
                if (bold) labelPara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(labelPara) { Padding = new System.Windows.Thickness(2, 1, 2, 1) });

                var valuePara = new Paragraph(new Run(value)) { TextAlignment = System.Windows.TextAlignment.Right, FontSize = fontSize, Margin = new System.Windows.Thickness(0) };
                if (bold) valuePara.FontWeight = FontWeights.Bold;
                row.Cells.Add(new TableCell(valuePara) { Padding = new System.Windows.Thickness(2, 1, 2, 1) });

                totalsGroup.Rows.Add(row);
            }

            AddTotalRow("Total Items Qty", ReturnItems.Where(i => i.ReturnQuantity > 0).Sum(i => i.ReturnQuantity).ToString(), bold: true);
            AddTotalRow("Total Refund", Region.Number(TotalReturnAmount), bold: true, fontSize: 12);

            totalsTable.RowGroups.Add(totalsGroup);
            doc.Blocks.Add(totalsTable);

            // --- FOOTER (per-client branding) ---
            var footer = ReceiptBranding.BuildFooter();
            footer.Margin = new System.Windows.Thickness(0, 20, 0, 0);
            doc.Blocks.Add(footer);

            return doc;
        }
    }

    public sealed class ReturnItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private int _originalQuantity;
        private int _returnQuantity;
        private decimal _costPrice;
        private decimal _unitPrice;
        private decimal _discountPercent;
        private string _discountType = "%";
        private decimal _lineQuantity;
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
                    CalculateTotal();
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
                    CalculateTotal();
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
                    OnPropertyChanged(nameof(DiscountDisplay));
                }
            }
        }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }

        /// <summary>
        /// How the original line was discounted: "%" (percentage) or a flat amount for the whole
        /// line ("AMT"; rows saved before the software was currency-neutral say "PKR").
        /// </summary>
        public string DiscountType
        {
            get => _discountType;
            set
            {
                if (SetProperty(ref _discountType, value))
                {
                    CalculateTotal();
                    OnPropertyChanged(nameof(DiscountDisplay));
                }
            }
        }

        /// <summary>The original line discount as the sale screen shows it: "10%" or "Rs. 25".</summary>
        public string DiscountDisplay =>
            _discountType == "%" ? $"{DiscountPercent:N0}%" : Region.MoneyWhole(DiscountPercent);

        /// <summary>The exact quantity on the original line (it can be fractional, e.g. 2.5 kg).</summary>
        public decimal LineQuantity
        {
            get => _lineQuantity;
            set
            {
                if (SetProperty(ref _lineQuantity, value))
                    CalculateTotal();
            }
        }

        private void CalculateTotal()
        {
            Total = (UnitPrice * ReturnQuantity) - ReturnedDiscount();
        }

        /// <summary>
        /// The part of the original line discount that belongs to the units being returned.
        /// A percentage applies to them as-is. A flat amount was taken off the whole line, so it
        /// is shared out per unit — returning every unit refunds exactly what was paid. (It used
        /// to be treated as a percentage, refunding the wrong amount.)
        /// </summary>
        private decimal ReturnedDiscount()
        {
            if (_discountType == "%")
                return (UnitPrice * ReturnQuantity * DiscountPercent) / 100;

            var lineQuantity = _lineQuantity > 0 ? _lineQuantity : _originalQuantity;
            if (ReturnQuantity <= 0 || lineQuantity <= 0) return 0;
            if (ReturnQuantity >= lineQuantity) return DiscountPercent;
            return Math.Round(DiscountPercent * ReturnQuantity / lineQuantity, 2, MidpointRounding.AwayFromZero);
        }
    }
}
