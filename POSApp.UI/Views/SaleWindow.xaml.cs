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

        // After a product is picked, the VM auto-adds it (and nulls SelectedProduct) via
        // its SelectedProduct setter. Setting SelectedItem = null on an editable ComboBox
        // does NOT clear the edit text, so the previous product's name lingers and breaks
        // the next type-to-search. Clear the text here so each new search starts blank.
        private void ProductCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ComboBox combo || combo.SelectedItem == null)
                return;
            if (!_viewModel.AutoAddItem)
                return;

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
