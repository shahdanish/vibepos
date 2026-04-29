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
    public sealed class SaleReturnViewModel : ViewModelBase
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IProductRepository _productRepository;

        private string _searchInvoiceNumber = string.Empty;
        private Sale? _originalSale;
        private string _returnInvoiceNumber = string.Empty;
        private DateTime _returnDate = DateTime.Now;
        private string _returnReason = string.Empty;
        private decimal _totalReturnAmount;

        public ObservableCollection<ReturnItemViewModel> ReturnItems { get; } = new();
        public ObservableCollection<Sale> SearchResults { get; } = new();

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
                {
                    LoadReturnItems(value);
                }
            }
        }

        public string ReturnInvoiceNumber
        {
            get => _returnInvoiceNumber;
            set => SetProperty(ref _returnInvoiceNumber, value);
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
        public ICommand PrintReturnCommand { get; }

        public SaleReturnViewModel(ISaleRepository saleRepository, IProductRepository productRepository)
        {
            _saleRepository = saleRepository;
            _productRepository = productRepository;

            SearchCommand = new RelayCommand(async _ => await SearchInvoice());
            ProcessReturnCommand = new RelayCommand(async _ => await ProcessReturn());
            CancelCommand = new RelayCommand(_ => Cancel());
            PrintReturnCommand = new RelayCommand(_ => PrintReturnReceipt());

            _ = GenerateReturnInvoiceNumber();
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

            try
            {
                var sale = await _saleRepository.GetByInvoiceNumberAsync(SearchInvoiceNumber);

                if (sale == null)
                {
                    NotificationHelper.ShowWarning($"Invoice '{SearchInvoiceNumber}' was not found in the system.", "Invoice Not Found");
                    return;
                }

                if (sale.SaleType == "Return")
                {
                    NotificationHelper.ShowWarning("This invoice is already a return transaction. You cannot process a return on a return.", "Invalid Operation");
                    return;
                }

                OriginalSale = sale;
                NotificationHelper.ShowInfo($"Invoice found successfully!\n\nCustomer: {sale.CustomerName}\nOriginal Amount: Rs.{sale.TotalBill:N2}", "Invoice Found");
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
                    OriginalQuantity = item.Quantity,
                    ReturnQuantity = 0, // User will specify
                    UnitPrice = item.UnitPrice,
                    CostPrice = item.CostPrice,
                    Total = 0
                });
            }
        }

        private async Task ProcessReturn()
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

            if (string.IsNullOrWhiteSpace(ReturnReason))
            {
                NotificationHelper.ValidationErrorCustom("Please provide a reason for this return.");
                return;
            }

            // Validate quantities
            foreach (var item in itemsToReturn)
            {
                if (item.ReturnQuantity > item.OriginalQuantity)
                {
                    NotificationHelper.ValidationErrorCustom($"Return quantity for '{item.ProductName}' cannot exceed the original quantity of {item.OriginalQuantity} units.");
                    return;
                }
            }

            try
            {
                // Create return sale
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
                    BillNote = $"Return for Invoice: {OriginalSale.InvoiceNumber}. Reason: {ReturnReason}",
                    TotalBill = -TotalReturnAmount, // Negative for return
                    ReceiveCash = -TotalReturnAmount,
                    Balance = 0
                };

                foreach (var item in itemsToReturn)
                {
                    returnSale.SaleItems.Add(new SaleItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = -item.ReturnQuantity, // Negative for return
                        CostPrice = item.CostPrice,
                        UnitPrice = item.UnitPrice,
                        Total = -item.Total // Negative for return
                    });

                    // Update stock - add back returned items
                    var product = await _productRepository.GetByProductIdAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.ReturnQuantity;
                        await _productRepository.UpdateAsync(product);
                    }
                }

                await _saleRepository.AddAsync(returnSale);

                NotificationHelper.ReturnProcessed(ReturnInvoiceNumber, TotalReturnAmount);

                // Reset form
                await GenerateReturnInvoiceNumber();
                OriginalSale = null;
                ReturnItems.Clear();
                SearchInvoiceNumber = string.Empty;
                ReturnReason = string.Empty;
                TotalReturnAmount = 0;
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("process return", ex.Message);
            }
        }

        private void Cancel()
        {
            // Close window logic in code-behind
        }

        private void PrintReturnReceipt()
        {
            if (OriginalSale == null)
            {
                NotificationHelper.ValidationErrorCustom("Please search and select an invoice before printing the receipt.");
                return;
            }

            try
            {
                FlowDocument printDoc = CreateProfessionalReturnInvoice();
                PrintDialog printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    printDoc.PageWidth = 280;
                    printDoc.PageHeight = double.NaN;
                    printDoc.ColumnWidth = 260;
                    printDoc.PagePadding = new Thickness(5);
                    printDoc.FontSize = 10;

                    printDialog.PrintDocument(
                        ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                        "Return Receipt Printing");

                    NotificationHelper.ShowSuccess($"Return Receipt {ReturnInvoiceNumber} sent to printer!");
                }
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("print return receipt", ex.Message);
            }
        }

        private FlowDocument CreateProfessionalReturnInvoice()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            doc.FontSize = 12;

            // --- HEADER ---
            Paragraph header = new Paragraph();
            header.TextAlignment = TextAlignment.Center;
            header.Inlines.Add(new Bold(new Run("Shah Jee Super Store")) { FontSize = 24 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 14 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("0332-3324911") { FontSize = 14 });
            doc.Blocks.Add(header);

            // --- TITLE ---
            Paragraph titlePara = new Paragraph(new Bold(new Run("Return Receipt")))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 16,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            doc.Blocks.Add(titlePara);

            // --- METADATA TABLE ---
            Table metaTable = new Table() { CellSpacing = 0 };
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });
            metaTable.Columns.Add(new TableColumn() { Width = new GridLength(1, GridUnitType.Star) });

            TableRowGroup metaGroup = new TableRowGroup();

            TableRow row1 = new TableRow();
            row1.Cells.Add(new TableCell(new Paragraph(new Run($"Return No: {ReturnInvoiceNumber}"))));
            row1.Cells.Add(new TableCell(new Paragraph(new Run($"Date: {ReturnDate:dd-MMM-yyyy}"))));
            metaGroup.Rows.Add(row1);

            TableRow row2 = new TableRow();
            row2.Cells.Add(new TableCell(new Paragraph(new Run($"Orig Invoice: {OriginalSale?.InvoiceNumber}"))));
            row2.Cells.Add(new TableCell(new Paragraph(new Run($"Customer: {OriginalSale?.CustomerName}"))));
            metaGroup.Rows.Add(row2);

            if (!string.IsNullOrWhiteSpace(ReturnReason))
            {
                TableRow row3 = new TableRow();
                row3.Cells.Add(new TableCell(new Paragraph(new Run($"Reason: {ReturnReason}"))) { ColumnSpan = 2 });
                metaGroup.Rows.Add(row3);
            }

            metaTable.RowGroups.Add(metaGroup);
            doc.Blocks.Add(metaTable);

            doc.Blocks.Add(new Paragraph(new Run("-----------------------------------------------------------------")));

            // --- ITEMS TABLE ---
            Table itemsTable = new Table() { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(2.5, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.6, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(0.9, GridUnitType.Star) });
            itemsTable.Columns.Add(new TableColumn() { Width = new GridLength(1.1, GridUnitType.Star) });

            TableRowGroup itemsGroup = new TableRowGroup();

            TableRow headerRow = new TableRow() { FontWeight = FontWeights.Bold, Background = Brushes.LightGray };
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Product Name"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Price"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
            itemsGroup.Rows.Add(headerRow);

            foreach (var item in ReturnItems.Where(i => i.ReturnQuantity > 0))
            {
                TableRow row = new TableRow();
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.ProductName))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.ReturnQuantity.ToString()))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.UnitPrice.ToString("N0")))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                row.Cells.Add(new TableCell(new Paragraph(new Run(item.Total.ToString("N2")))) { BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black });
                itemsGroup.Rows.Add(row);
            }

            itemsTable.RowGroups.Add(itemsGroup);
            doc.Blocks.Add(itemsTable);

            // --- TOTALS ---
            Paragraph refundPara = new Paragraph();
            refundPara.TextAlignment = TextAlignment.Right;
            refundPara.Margin = new Thickness(0, 10, 0, 0);
            refundPara.Inlines.Add(new Bold(new Run($"TOTAL REFUND: Rs.{TotalReturnAmount:N2}")) { FontSize = 16, Foreground = Brushes.Red });
            doc.Blocks.Add(refundPara);

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

    public sealed class ReturnItemViewModel : ViewModelBase
    {
        private string _productId = string.Empty;
        private string _productName = string.Empty;
        private int _originalQuantity;
        private int _returnQuantity;
        private decimal _costPrice;
        private decimal _unitPrice;
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
                {
                    Total = value * UnitPrice;
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
            set => SetProperty(ref _unitPrice, value);
        }

        public decimal Total
        {
            get => _total;
            set => SetProperty(ref _total, value);
        }
    }
}
