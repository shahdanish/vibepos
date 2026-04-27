using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace POSApp.UI.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private string _invoiceText;
        private bool _useSmallBillFormat;
        private Action? _onPrintComplete;

        public PrintPreviewWindow(string invoiceText, FlowDocument flowDoc, bool useSmallBillFormat, Action? onPrintComplete = null)
        {
            InitializeComponent();
            _invoiceText = invoiceText;
            _useSmallBillFormat = useSmallBillFormat;
            _onPrintComplete = onPrintComplete;

            // Show the formatted document in the viewer
            DocViewer.Document = flowDoc;

            if (_useSmallBillFormat)
            {
                flowDoc.PageWidth = 302;
                flowDoc.ColumnWidth = 280;
            }

            Title = useSmallBillFormat ? "Thermal Receipt Preview" : "Invoice Preview";
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Build a completely independent FlowDocument for printing
                FlowDocument printDoc = BuildPrintDocument(_invoiceText);

                // Use the standard WPF PrintDialog
                PrintDialog printDialog = new PrintDialog();

                if (_useSmallBillFormat)
                {
                    printDoc.PageWidth = 302;
                    printDoc.PageHeight = 5000;
                    printDoc.ColumnWidth = 280;
                }
                else
                {
                    // A4 sizing — set BEFORE showing dialog
                    printDoc.PageWidth = 816;  // 8.5 inches * 96 DPI
                    printDoc.PageHeight = 1056; // 11 inches * 96 DPI
                    printDoc.ColumnWidth = 816;
                    printDoc.PagePadding = new Thickness(50);
                }

                if (printDialog.ShowDialog() == true)
                {
                    // Update page dimensions to match chosen printer
                    if (!_useSmallBillFormat)
                    {
                        printDoc.PageWidth = printDialog.PrintableAreaWidth;
                        printDoc.PageHeight = printDialog.PrintableAreaHeight;
                        printDoc.ColumnWidth = printDialog.PrintableAreaWidth;
                    }

                    // Print with the document paginator
                    printDialog.PrintDocument(
                        ((IDocumentPaginatorSource)printDoc).DocumentPaginator,
                        "Invoice Printing");

                    MessageBox.Show("Invoice sent to printer!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    _onPrintComplete?.Invoke();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to print: {ex.Message}", "Print Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Creates a brand-new FlowDocument from the raw invoice text.
        /// This document is completely independent from the one shown in the preview viewer,
        /// so there are no "document already in use" issues.
        /// </summary>
        private FlowDocument BuildPrintDocument(string text)
        {
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                LineHeight = 18,
                PagePadding = new Thickness(50)
            };

            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                var para = new Paragraph(new Run(line))
                {
                    Margin = new Thickness(0, 1, 0, 1)
                };

                // Bold headers / totals
                if (line.Contains("SHAH JEE") || line.Contains("====") ||
                    line.StartsWith("Inv:") || line.StartsWith("Items:") ||
                    line.Contains("Total") || line.Contains("Cash") ||
                    line.Contains("Balance") || line.Contains("----"))
                {
                    para.FontWeight = FontWeights.Bold;
                }

                doc.Blocks.Add(para);
            }

            return doc;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _onPrintComplete?.Invoke();
            DialogResult = false;
            Close();
        }
    }
}
