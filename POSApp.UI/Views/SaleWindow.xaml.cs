using System.Windows;
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
                // Recreate partner if it was closed (X button)
                if (_wholeSalePartner == null || !_wholeSalePartner.IsLoaded)
                {
                    _wholeSalePartner = App.Services!.GetRequiredService<WholeSaleWindow>();
                    var wsVm = (WholeSaleViewModel)_wholeSalePartner.DataContext;

                    // Seed the wholesale cart from the current sale state on first open
                    wsVm.LoadState(viewModel);

                    // Wire the partner's switch button to come straight back here
                    wsVm.SwitchMode = () =>
                    {
                        _wholeSalePartner.Hide();
                        Show();
                    };
                }

                _wholeSalePartner.Title = "Wholesale Sale - Shah Jee Super Store";
                _wholeSalePartner.Show();
                Hide();
            };

            // Enable keyboard shortcuts
            KeyDown += SaleWindow_KeyDown;
        }

        private void SaleWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check for Ctrl+S - New Sale
            if (e.Key == System.Windows.Input.Key.S &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                _viewModel.NewCommand.Execute(null);
                return;
            }

            // Check for Ctrl+W - Undo Last Sale (reverse last transaction)
            if (e.Key == System.Windows.Input.Key.W &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                // Implement undo logic here if needed
                MessageBox.Show("Undo sale feature requires additional implementation.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        private void Calculator_Click(object sender, RoutedEventArgs e)
        {
            CalculatorWindow.ShowCalculator();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
