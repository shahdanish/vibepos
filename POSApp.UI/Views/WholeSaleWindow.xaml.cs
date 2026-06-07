using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

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
                quickWindow.Title = "Quick Wholesale Sale - Shah Jee Super Store";
                quickWindow.Show();
            };

            // Default standalone switch (overridden when opened as a partner from SaleWindow).
            // Creates a Sale partner once and reuses it; never transfers cart items.
            viewModel.SwitchMode = () =>
            {
                if (_salePartner == null || !_salePartner.IsLoaded)
                {
                    _salePartner = App.Services!.GetRequiredService<SaleWindow>();
                    var saleVm = (SaleViewModel)_salePartner.DataContext;

                    // Wire the sale partner's switch button to come back here
                    saleVm.SwitchMode = () =>
                    {
                        _salePartner.Hide();
                        Show();
                    };
                }

                _salePartner.Title = "Sale - Shah Jee Super Store";
                _salePartner.Show();
                Hide();
            };
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
