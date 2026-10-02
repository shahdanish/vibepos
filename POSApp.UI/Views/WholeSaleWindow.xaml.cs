using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    public partial class WholeSaleWindow : Window
    {
        private readonly WholeSaleViewModel _viewModel;
        private SaleWindow? _salePartner;

        public WholeSaleWindow(WholeSaleViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;

            viewModel.OpenQuickSaleWindow = () =>
            {
                var quickWindow = App.Services!.GetRequiredService<WholeSaleWindow>();
                quickWindow.Title = ShopTitleExtension.Build("Quick Wholesale Sale");
                quickWindow.Show();
            };

            // Default standalone switch — creates a Sale partner once and reuses it
            viewModel.SwitchMode = () =>
            {
                if (_salePartner == null || !_salePartner.IsLoaded)
                {
                    _salePartner = App.Services!.GetRequiredService<SaleWindow>();
                    var saleVm = (SaleViewModel)_salePartner.DataContext;

                    saleVm.SwitchMode = () =>
                    {
                        _salePartner.Hide();
                        Show();
                        Activate();
                    };

                    // Restore this Wholesale window when Sale is closed via X or CLOSE
                    _salePartner.Closed += (s, e) =>
                    {
                        if (!IsVisible) { Show(); Activate(); }
                    };
                }

                _salePartner.Show();
                _salePartner.Activate();
                Hide();
            };

            PreviewKeyDown += WholeSaleWindow_KeyDown;

            // US front store: ID checks and the pseudoephedrine logbook.
            _viewModel.RequestIdCheck = (age, product) => IdCheckDialog.Ask(this, age, product);
            _viewModel.RequestPseLog = lines => PseLogDialog.Ask(this, lines);
            CardReader.Attach(this, _viewModel);

            // Pick up quick keys starred in Products (or in the other sale window) meanwhile.
            Activated += async (_, _) => await _viewModel.LoadQuickKeysAsync();

            // On short screens only a line or two of the cart is visible: bring the line a
            // scan, tile or F-key just added (or added one more to) into view.
            _viewModel.CartLineChanged += line =>
                Dispatcher.BeginInvoke(() => ItemsGrid.ScrollIntoView(line), DispatcherPriority.Background);
        }

        private void WholeSaleWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

            if (ctrl && e.Key == Key.N) { e.Handled = true; _viewModel.NewCommand.Execute(null); }
            else if (ctrl && e.Key == Key.P) { e.Handled = true; _viewModel.PrintCommand.Execute(null); }
            else if (ctrl && e.Key == Key.Enter) { e.Handled = true; _viewModel.SaveCommand.Execute(null); }
            else if (ctrl && e.Key == Key.W) { e.Handled = true; _viewModel.SwitchModeCommand.Execute(null); }
            else if (ctrl && e.Key == Key.Q) { e.Handled = true; _viewModel.QuickSaleCommand.Execute(null); }
            else if (ctrl && e.Key == Key.M) { e.Handled = true; CalculatorWindow.ShowCalculator(); }
            else if (ctrl && e.Key == Key.T && _viewModel.IsUsCheckout) { e.Handled = true; SplitPaymentDialog.Run(this, _viewModel); }
            else if (ctrl && e.Key == Key.D && CashDrawer.IsEnabled) { e.Handled = true; OpenDrawerNoSale(); }
            else if (TryHandleQuickKeyHotKey(e)) { e.Handled = true; }
            else if (e.Key == Key.Escape) { e.Handled = true; Close(); }
        }

        private void SplitPayment_Click(object sender, RoutedEventArgs e) => SplitPaymentDialog.Run(this, _viewModel);

        /// <summary>Ctrl+D: open the drawer without a sale (making change) — cash-register managers only.</summary>
        private static void OpenDrawerNoSale()
        {
            if (!SessionManager.HasPermission(POSApp.Core.Entities.Permissions.ShiftsManage))
            {
                NotificationHelper.ValidationErrorCustom("Only users who manage the cash register can open the drawer without a sale.");
                return;
            }
            var error = CashDrawer.Open();
            if (error != null) NotificationHelper.OperationFailed("open the cash drawer", error);
        }

        // F1–F12 add the first twelve quick keys — except inside the cart grid, where F2
        // edits a cell. Returns true when a quick key was added.
        private bool TryHandleQuickKeyHotKey(KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (Keyboard.Modifiers != ModifierKeys.None || key < Key.F1 || key > Key.F12) return false;
            if (Keyboard.FocusedElement is DependencyObject focused && IsWithin(focused, ItemsGrid)) return false;
            return _viewModel.TryAddQuickKey(key - Key.F1);
        }

        private static bool IsWithin(DependencyObject element, DependencyObject container)
        {
            for (var d = element; d != null;
                 d = d is Visual ? VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            {
                if (d == container) return true;
            }
            return false;
        }

        // Guard so an Enter keypress that closes the dropdown does not commit twice
        // (once in PreviewKeyDown and again in DropDownClosed).
        private bool _committedFromKeyboard;

        // Wholesale mirrors the NORMAL sale screen: the product is added ONLY on an explicit
        // selection. Manual typing (TextSearch) merely highlights the first match — it must
        // NOT add. Enter commits the currently matched/highlighted product; clicking a
        // dropdown item commits it via DropDownClosed. Barcode scanning uses the separate
        // Barcode field (ScanCommand) and is unaffected.
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
