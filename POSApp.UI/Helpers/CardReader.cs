using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.Payments;
using POSApp.UI.ViewModels;
using POSApp.UI.Views;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Connects a sale window to the integrated card reader set up in Business Settings (US).
    /// With no reader set up, card tenders stay as they were: keyed on a separate card machine.
    /// </summary>
    public static class CardReader
    {
        public static void Attach(Window window, SaleViewModel vm)
        {
            // Re-read on every activation, so a reader set up meanwhile is used straight away.
            window.Loaded += async (_, _) => await RefreshAsync(window, vm);
            window.Activated += async (_, _) => await RefreshAsync(window, vm);

            window.Closing += async (_, e) =>
            {
                if (!vm.HasUnsavedCardCharges) return;
                e.Cancel = true;
                if (!NotificationHelper.Confirm(
                        "A card was charged for this bill, but the sale has not been saved.\n\n" +
                        "Close anyway and refund the card?", "Card charged")) return;
                await vm.DiscardCardChargesAsync();
                window.Close();
            };
        }

        private static async Task RefreshAsync(Window window, SaleViewModel vm)
        {
            ICardTerminal? terminal = null;
            if (vm.IsUsCheckout)
            {
                try
                {
                    using var scope = App.Services!.CreateScope();
                    terminal = await scope.ServiceProvider.GetRequiredService<CardTerminalFactory>().CreateAsync();
                }
                catch (Exception ex)
                {
                    CrashLog.Write(ex, "Card reader set-up");
                }
            }

            if (terminal == null)
            {
                vm.RequestCardCharge = null;
                vm.VoidCardCharge = null;
                return;
            }
            vm.RequestCardCharge = (amount, description) => CardChargeDialog.Run(window, terminal, amount, description);
            vm.VoidCardCharge = async (reference, amount) => (await terminal.RefundAsync(reference, amount)).Succeeded;
        }
    }
}
