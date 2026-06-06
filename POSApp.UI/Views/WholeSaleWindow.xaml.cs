using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class WholeSaleWindow : Window
    {
        private readonly WholeSaleViewModel _viewModel;

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

            // Default standalone switch (overridden when opened as a partner from SaleWindow)
            viewModel.SwitchMode = () =>
            {
                var saleWindow = App.Services!.GetRequiredService<SaleWindow>();
                var saleVm = (SaleViewModel)saleWindow.DataContext;
                saleVm.LoadState(viewModel);
                saleWindow.Title = "Sale - Shah Jee Super Store";
                saleWindow.Show();
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
