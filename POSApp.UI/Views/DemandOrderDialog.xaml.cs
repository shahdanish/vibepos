using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class DemandOrderDialog : Window
    {
        private readonly List<DemandOrderItem> _items;

        public DemandOrderDialog(List<DemandOrderItem> items)
        {
            InitializeComponent();
            _items = items;

            // Assign display serial numbers (1..n) for the grid.
            for (int n = 0; n < _items.Count; n++)
                _items[n].SerialNo = n + 1;

            ItemsGrid.ItemsSource = _items;
            TodayDateRun.Text = DateTime.Now.ToString("dd-MMM-yyyy");
            UpdateTotalBill();
        }

        private void UpdateTotalBill()
        {
            TotalBillRun.Text = _items.Sum(x => x.LineTotal).ToString("N2");
        }

        private void ItemsGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Recompute after the bound value commits.
            Dispatcher.BeginInvoke(new Action(UpdateTotalBill), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = BuildPrintDocument();
                var dlg = new PrintDialog();

                bool small = POSApp.UI.Helpers.SettingsManager.LoadSettings().UseSmallBillFormat;
                if (small)
                {
                    doc.PageWidth = 280;
                    doc.PageHeight = double.NaN;
                    doc.ColumnWidth = 260;
                    doc.PagePadding = new Thickness(5);
                    doc.FontSize = 10;
                }
                else
                {
                    doc.PageWidth = dlg.PrintableAreaWidth;
                    doc.PageHeight = dlg.PrintableAreaHeight;
                    doc.ColumnWidth = dlg.PrintableAreaWidth;
                    doc.PagePadding = new Thickness(40);
                }

                dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Demand Order");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument BuildPrintDocument()
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextAlignment = TextAlignment.Left
            };

            // Store header
            var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
            header.Inlines.Add(new Bold(new Run("Shahjee super store")) { FontSize = 22 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("Dillewali, Mianwali") { FontSize = 13 });
            header.Inlines.Add(new LineBreak());
            header.Inlines.Add(new Run("0332-3324911") { FontSize = 13 });
            doc.Blocks.Add(header);

            // Title
            doc.Blocks.Add(new Paragraph(new Bold(new Run("📋 Demand / Purchase Order")))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 15,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 2, 0, 4)
            });

            // Date
            doc.Blocks.Add(new Paragraph(new Run($"Date: {DateTime.Now:dd-MMM-yyyy  hh:mm tt}"))
            {
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Print ONLY checked rows. The Price column is printed BLANK for the
            // recipient (supplier) to fill in manually. The checkbox column is not printed.
            var printItems = _items.Where(x => x.IsSelected).ToList();

            // Items table — columns: S.No | Item | Qty | Price (Price left blank).
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) });  // S.No
            table.Columns.Add(new TableColumn { Width = new GridLength(2.2, GridUnitType.Star) });  // Item
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });  // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(2.6, GridUnitType.Star) });  // Price (blank)

            var group = new TableRowGroup();

            // vPad controls the row height. Body rows use a taller pad so the blank
            // Price cell has enough vertical room for the supplier to write prices by hand.
            TableCell Cell(string text, TextAlignment align, bool bold = false, bool isHeader = false, double vPad = 2)
            {
                var para = new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) };
                if (bold || isHeader) para.FontWeight = FontWeights.Bold;
                var cell = new TableCell(para) { Padding = new Thickness(3, vPad, 3, vPad), BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black };
                return cell;
            }

            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(Cell("S.No",  TextAlignment.Center, isHeader: true));
            headerRow.Cells.Add(Cell("Item",  TextAlignment.Left,   isHeader: true));
            headerRow.Cells.Add(Cell("Qty",   TextAlignment.Center, isHeader: true));
            headerRow.Cells.Add(Cell("Price", TextAlignment.Right,  isHeader: true));
            group.Rows.Add(headerRow);

            int i = 1;
            foreach (var item in printItems)
            {
                var row = new TableRow();
                row.Cells.Add(Cell(i++.ToString(), TextAlignment.Center, vPad: 12));
                row.Cells.Add(Cell(item.ProductName, TextAlignment.Left, vPad: 12));
                row.Cells.Add(Cell(item.OrderQuantity.ToString(), TextAlignment.Center, bold: true, vPad: 12));
                row.Cells.Add(Cell(string.Empty, TextAlignment.Right, vPad: 12)); // Price left blank — filled by hand
                group.Rows.Add(row);
            }

            table.RowGroups.Add(group);
            doc.Blocks.Add(table);

            // Footer
            doc.Blocks.Add(new Paragraph(new Run("─────────────────────────────────")) { Margin = new Thickness(0, 4, 0, 4) });
            doc.Blocks.Add(new Paragraph(new Run("Prepared by: _________________   Date: _______________"))
            {
                Margin = new Thickness(0, 0, 0, 2)
            });
            doc.Blocks.Add(new Paragraph(new Run("Authorized by: ______________   Signature: ___________")));

            return doc;
        }
    }
}
