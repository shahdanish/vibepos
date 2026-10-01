using System.Windows;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;
using POSApp.Core.Services;

namespace POSApp.UI.Views
{
    /// <summary>
    /// First launch of a Store install: shop name/currency and the owner's login. Replaces the
    /// demo accounts from the seed data so no two installs share a known password.
    /// </summary>
    public partial class FirstRunSetupWindow : Window
    {
        /// <param name="RegionCode">Country whose full preset (dates, spacing, receipt decimals) applies, or Other.</param>
        private sealed record CurrencyOption(string Label, string Symbol, string Name, NumberWordStyle Words, string RegionCode);

        // US Dollar first: it's the default for new Store installs.
        private static readonly CurrencyOption[] Currencies =
        {
            new("$ — US Dollar (United States)", "$",   "Dollars",  NumberWordStyle.International, RegionCodes.UnitedStates),
            new("€ — Euro",                "€",   "Euros",    NumberWordStyle.International, RegionCodes.Other),
            new("£ — British Pound",       "£",   "Pounds",   NumberWordStyle.International, RegionCodes.Other),
            new("AED — UAE Dirham",        "AED", "Dirhams",  NumberWordStyle.International, RegionCodes.Other),
            new("SAR — Saudi Riyal",       "SAR", "Riyals",   NumberWordStyle.International, RegionCodes.Other),
            new("Rs. — Pakistani Rupee",   "Rs.", "Rupees",   NumberWordStyle.SouthAsian,    RegionCodes.Pakistan),
            new("₹ — Indian Rupee",        "₹",   "Rupees",   NumberWordStyle.SouthAsian,    RegionCodes.Other),
            new("Tk — Bangladeshi Taka",   "Tk",  "Taka",     NumberWordStyle.SouthAsian,    RegionCodes.Other),
        };

        private readonly IFirstRunSetupService _setup;

        public FirstRunSetupWindow(IFirstRunSetupService setup)
        {
            InitializeComponent();
            _setup = setup;

            Currency.ItemsSource = Currencies;
            Currency.SelectedItem = Currencies[0]; // US Dollar

            var branding = ReceiptBranding.Current;
            if (branding.StoreName != new ReceiptBrandingSettings().StoreName)
                ShopName.Text = branding.StoreName;

            Loaded += (_, _) => ShopName.Focus();
        }

        private async void Finish_Click(object sender, RoutedEventArgs e)
        {
            var error = Validate();
            if (error != null)
            {
                ShowError(error);
                return;
            }

            FinishButton.IsEnabled = false;
            ErrorPanel.Visibility = Visibility.Collapsed;
            try
            {
                // Shop identity → receipts and window titles.
                var branding = ReceiptBranding.Current;
                branding.StoreName = ShopName.Text.Trim();
                branding.StorePhone = ShopPhone.Text.Trim();
                branding.StoreAddress = ShopAddress.Text.Trim();
                ReceiptBranding.Save(branding);

                if (Currency.SelectedItem is CurrencyOption c)
                {
                    // Pakistan and the US get their whole preset; any other currency only
                    // changes the money fields and keeps the remaining defaults.
                    var region = RegionSettingsData.PresetFor(c.RegionCode) ?? Region.Current.Clone();
                    region.RegionCode = c.RegionCode;
                    region.CurrencySymbol = c.Symbol;
                    region.CurrencyName = c.Name;
                    region.NumberWords = c.Words;
                    Region.Save(region);
                }

                await _setup.CompleteAsync(new FirstRunSetupRequest(
                    AdminUsername.Text.Trim(),
                    AdminPassword.Password,
                    KeepSamples.IsChecked == true));

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError($"Setup could not be saved: {ex.Message}");
                FinishButton.IsEnabled = true;
            }
        }

        private string? Validate()
        {
            if (string.IsNullOrWhiteSpace(ShopName.Text)) return "Please enter your shop name.";
            if (AdminUsername.Text.Trim().Length < 3) return "The username needs at least 3 characters.";
            if (AdminPassword.Password.Length < 6) return "The password needs at least 6 characters.";
            if (AdminPassword.Password != AdminPasswordConfirm.Password) return "The two passwords don't match.";
            return null;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
