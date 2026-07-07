using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SaleWindow : Window
    {
        private readonly SaleViewModel _viewModel;
        private WholeSaleWindow? _wholeSalePartner;

        public SaleWindow(SaleViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;

            viewModel.OpenQuickSaleWindow = () =>
            {
                var quickWindow = App.Services!.GetRequiredService<SaleWindow>();
                quickWindow.Title = "Quick Sale - Shah Jee Super Store";
                quickWindow.Show();
            };

            viewModel.SwitchMode = () =>
            {
                if (_wholeSalePartner == null || !_wholeSalePartner.IsLoaded)
                {
                    _wholeSalePartner = App.Services!.GetRequiredService<WholeSaleWindow>();
                    var wsVm = (WholeSaleViewModel)_wholeSalePartner.DataContext;

                    wsVm.SwitchMode = () =>
                    {
                        _wholeSalePartner.Hide();
                        Show();
                        Activate();
                    };

                    // Restore this Sale window when Wholesale is closed via X or CLOSE
                    _wholeSalePartner.Closed += (s, e) =>
                    {
                        if (!IsVisible) { Show(); Activate(); }
                    };
                }

                _wholeSalePartner.Show();
                _wholeSalePartner.Activate();
                Hide();
            };

            PreviewKeyDown += SaleWindow_KeyDown;
        }

        private void SaleWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

            if (ctrl && e.Key == Key.N) { e.Handled = true; _viewModel.NewCommand.Execute(null); }
            else if (ctrl && e.Key == Key.P) { e.Handled = true; _viewModel.PrintCommand.Execute(null); }
            else if (ctrl && e.Key == Key.Enter) { e.Handled = true; _viewModel.SaveCommand.Execute(null); }
            else if (ctrl && e.Key == Key.W) { e.Handled = true; _viewModel.SwitchModeCommand.Execute(null); }
            else if (ctrl && e.Key == Key.Q) { e.Handled = true; _viewModel.QuickSaleCommand.Execute(null); }
            else if (ctrl && e.Key == Key.M) { e.Handled = true; CalculatorWindow.ShowCalculator(); }
            else if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        }

        // Guard so an Enter keypress that closes the dropdown does not commit twice
        // (once in PreviewKeyDown and again in DropDownClosed).
        private bool _committedFromKeyboard;

        // On the NORMAL sale screen the product is added ONLY on an explicit selection.
        // Manual typing (TextSearch) merely highlights the first match — it must NOT add.
        // Enter commits the currently matched/highlighted product; clicking a dropdown
        // item commits it via DropDownClosed. Barcode scanning uses the separate Barcode
        // field (ScanCommand) and is unaffected.
        private void ProductCombo_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.ComboBox combo)
                return;

            // Escape cancels: suppress the commit that DropDownClosed would otherwise do.
            if (e.Key == Key.Escape && combo.IsDropDownOpen)
            {
                _committedFromKeyboard = true; // reuse the skip-next-DropDownClosed guard
                return;
            }

            if (e.Key != Key.Enter)
                return;

            // Enter with the dropdown open closes it; let DropDownClosed do the commit.
            if (combo.IsDropDownOpen)
                return;

            if (_viewModel.SelectedProduct == null)
                return;

            e.Handled = true;
            _committedFromKeyboard = true;
            CommitSelectedProduct(combo);
        }

        // Fires when the user clicks a dropdown item (or Enter closes an open dropdown).
        private void ProductCombo_DropDownClosed(object sender, EventArgs e)
        {
            if (sender is not System.Windows.Controls.ComboBox combo)
                return;

            if (_committedFromKeyboard)
            {
                _committedFromKeyboard = false;
                return;
            }

            if (_viewModel.SelectedProduct == null)
                return;

            CommitSelectedProduct(combo);
        }

        // Adds the selected product to the cart, then clears the search box so the next
        // type-to-search starts blank (nulling SelectedItem alone does not clear the edit
        // text on an editable ComboBox).
        private void CommitSelectedProduct(System.Windows.Controls.ComboBox combo)
        {
            _viewModel.AddItemCommand.Execute(null);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                combo.SelectedItem = null;
                combo.Text = string.Empty;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Calculator_Click(object sender, RoutedEventArgs e)
            => CalculatorWindow.ShowCalculator();

        private void Exit_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
