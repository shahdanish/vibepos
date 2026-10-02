using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Infrastructure.Payments;
using POSApp.UI.Helpers;
using POSApp.UI.ViewModels;

namespace POSApp.UI.Views
{
    public partial class SaleReturnWindow : Window
    {
        public SaleReturnWindow(SaleReturnViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // US: card sales taken on the integrated reader are refunded back to the same card.
            Loaded += async (_, _) =>
            {
                if (!Region.IsUnitedStates) return;
                try
                {
                    using var scope = App.Services!.CreateScope();
                    var terminal = await scope.ServiceProvider.GetRequiredService<CardTerminalFactory>().CreateAsync();
                    if (terminal != null)
                        viewModel.RefundToCard = (reference, amount) => terminal.RefundAsync(reference, amount);
                }
                catch (Exception ex)
                {
                    CrashLog.Write(ex, "Card reader set-up");
                }
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
