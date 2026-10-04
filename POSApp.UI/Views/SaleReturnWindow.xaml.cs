using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.Core.Services;
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
                viewModel.RestoreStoreTender = refund => RestoreStoreTenderAsync(viewModel, refund);
                viewModel.ReduceIssuedGiftCard = (code, amount) => ReduceIssuedGiftCardAsync(code, amount);
                viewModel.ClawBackEarnedLoyalty = () => ClawBackEarnedLoyaltyAsync(viewModel);
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

        private static async Task<string?> ReduceIssuedGiftCardAsync(string code, decimal face)
        {
            using var scope = App.Services!.CreateScope();
            var gifts = scope.ServiceProvider.GetRequiredService<IGiftCardRepository>();
            var card = await gifts.GetByCodeAsync(code);
            if (card == null) return $"Gift card {code} was not found, so it cannot be returned.";
            var error = GiftCardRules.ReverseIssue(card, face);
            if (error != null) return error;
            await gifts.UpdateAsync(card);
            return null;
        }

        private static async Task<string?> RestoreStoreTenderAsync(SaleReturnViewModel viewModel, SalePayment refund)
        {
            using var scope = App.Services!.CreateScope();
            var gifts = scope.ServiceProvider.GetRequiredService<IGiftCardRepository>();
            var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var original = viewModel.OriginalSale;

            if (refund.Method == PaymentMethods.GiftCard)
            {
                var card = await gifts.GetByCodeAsync(refund.Reference ?? string.Empty);
                if (card == null) return $"Gift card {refund.Reference} was not found, so the balance was not put back.";
                GiftCardRules.Credit(card, refund.Amount);
                await gifts.UpdateAsync(card);
                return null;
            }

            if (refund.Method == PaymentMethods.Loyalty)
            {
                if (original?.CustomerId is not int customerId)
                    return "This sale has no customer, so the points were not put back.";
                var customer = await customers.GetByIdAsync(customerId);
                if (customer == null) return "The customer was not found, so the points were not put back.";

                var restored = int.TryParse(refund.Reference, out var points) ? points : 0;
                customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints + restored);
                customer.ModifiedDate = DateTime.Now;
                await customers.UpdateAsync(customer);
                return null;
            }

            return null;
        }

        /// <summary>
        /// Takes back points earned on the merchandise being returned. A gift-card line does not
        /// earn points, so returning only that line leaves the points alone.
        /// </summary>
        private static async Task<string?> ClawBackEarnedLoyaltyAsync(SaleReturnViewModel viewModel)
        {
            var original = viewModel.OriginalSale;
            if (original?.CustomerId is not int customerId) return null;

            using var scope = App.Services!.CreateScope();
            var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var shopText = scope.ServiceProvider.GetRequiredService<IShopTextStore>();
            var customer = await customers.GetByIdAsync(customerId);
            if (customer == null)
                return "The customer was not found, so loyalty points earned on this sale were not taken back.";
            if (!customer.LoyaltyEnrolled) return null;

            var settings = await shopText.GetAsync();
            var clawBack = viewModel.EarnedPointsToClawBack(settings.PointsPerDollar);
            if (clawBack <= 0) return null;
            customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - clawBack);
            customer.ModifiedDate = DateTime.Now;
            await customers.UpdateAsync(customer);
            return null;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
