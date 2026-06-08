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

            // Items table — columns: S.N.O | Product | QTY | Rs
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0, 1, 0, 1) };
            table.Columns.Add(new TableColumn { Width = new GridLength(0.6, GridUnitType.Star) });  // S.N.O
            table.Columns.Add(new TableColumn { Width = new GridLength(3.0, GridUnitType.Star) });  // Product
            table.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });  // QTY
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });  // Rs

            var group = new TableRowGroup();

            TableCell Cell(string text, TextAlignment align, bool bold = false, bool isHeader = false)
            {
                var para = new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) };
                if (bold || isHeader) para.FontWeight = FontWeights.Bold;
                var cell = new TableCell(para) { Padding = new Thickness(3, 2, 3, 2), BorderThickness = new Thickness(0.5), BorderBrush = Brushes.Black };
                return cell;
            }

            var headerRow = new TableRow { Background = Brushes.LightGray };
            headerRow.Cells.Add(Cell("S.N.O",   TextAlignment.Center, isHeader: true));
            headerRow.Cells.Add(Cell("Product", TextAlignment.Left,   isHeader: true));
            headerRow.Cells.Add(Cell("QTY",     TextAlignment.Center, isHeader: true));
            headerRow.Cells.Add(Cell("Rs",      TextAlignment.Right,  isHeader: true));
            group.Rows.Add(headerRow);

            int i = 1;
            foreach (var item in _items)
            {
                var row = new TableRow();
                row.Cells.Add(Cell(i++.ToString(), TextAlignment.Center));
                row.Cells.Add(Cell(item.ProductName, TextAlignment.Left));
                row.Cells.Add(Cell(item.OrderQuantity.ToString(), TextAlignment.Center, bold: true));
                row.Cells.Add(Cell($"{item.LineTotal:N2}", TextAlignment.Right));
                group.Rows.Add(row);
            }

            table.RowGroups.Add(group);
            doc.Blocks.Add(table);

            // Total Bill line
            decimal totalBill = _items.Sum(x => x.LineTotal);
            doc.Blocks.Add(new Paragraph(new Bold(new Run($"Total Bill:   Rs. {totalBill:N2}")))
            {
                TextAlignment = TextAlignment.Right,
                FontSize = 14,
                Margin = new Thickness(0, 6, 0, 0)
            });

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
