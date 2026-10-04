using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using POSApp.Core.Interfaces;
using POSApp.Infrastructure.SampleData;
using POSApp.UI.Helpers;
using POSApp.Core.Services;

namespace POSApp.UI.Views
{
    /// <summary>
    /// First launch of a new install: shop name/currency, the owner's login and optional sample
    /// data. Replaces the demo accounts from the seed data so no two installs share a known password.
    /// </summary>
    public partial class FirstRunSetupWindow : Window
    {
        /// <param name="RegionCode">Country whose full preset (dates, spacing, receipt decimals) applies, or Other.</param>
        private sealed record CurrencyOption(string Label, string Symbol, string Name, string Code, NumberWordStyle Words, string RegionCode);

        // US Dollar first: it's the default for new Store installs.
        private static readonly CurrencyOption[] Currencies =
        {
            new("$ — US Dollar (United States)", "$",   "Dollars",  "USD", NumberWordStyle.International, RegionCodes.UnitedStates),
            new("€ — Euro",                "€",   "Euros",    "EUR", NumberWordStyle.International, RegionCodes.Other),
            new("£ — British Pound",       "£",   "Pounds",   "GBP", NumberWordStyle.International, RegionCodes.Other),
            new("AED — UAE Dirham",        "AED", "Dirhams",  "AED", NumberWordStyle.International, RegionCodes.Other),
            new("SAR — Saudi Riyal",       "SAR", "Riyals",   "SAR", NumberWordStyle.International, RegionCodes.Other),
            new("Rs. — Pakistani Rupee",   "Rs.", "Rupees",   "PKR", NumberWordStyle.SouthAsian,    RegionCodes.Pakistan),
            new("₹ — Indian Rupee",        "₹",   "Rupees",   "INR", NumberWordStyle.SouthAsian,    RegionCodes.Other),
            new("Tk — Bangladeshi Taka",   "Tk",  "Taka",     "BDT", NumberWordStyle.SouthAsian,    RegionCodes.Other),
        };

        private readonly IFirstRunSetupService _setup;
        private readonly ITaxRepository _tax;
        private readonly IShopTextStore _shopText;
        private ObservableCollection<PhraseRow> _phrases = new();
        private bool _loading = true;

        public FirstRunSetupWindow(IFirstRunSetupService setup, ITaxRepository tax, IShopTextStore shopText)
        {
            InitializeComponent();
            _setup = setup;
            _tax = tax;
            _shopText = shopText;

            Title = $"Welcome to {ProductBranding.Name}";
            ShopLanguage.SelectedIndex = 0;
            LoadPhraseGrid(ShopPhrases.English);
            RefreshSampleLabel();

            Currency.ItemsSource = Currencies;
            Currency.SelectedItem = Currencies[0]; // US Dollar
            _loading = false;

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
                    region.CurrencyCode = c.Code;
                    region.NumberWords = c.Words;
                    Region.Save(region);

                    // US shops get the usual tax categories, charged at the rate entered.
                    if (RegionCodes.IsUnitedStates(c.RegionCode))
                        await _tax.EnsureUsDefaultsAsync(ParsedTaxRate() ?? 0m);
                }

                await SaveWordingAsync();

                var result = await _setup.CompleteAsync(new FirstRunSetupRequest(
                    AdminUsername.Text.Trim(),
                    AdminPassword.Password,
                    LoadSamples.IsChecked == true));

                if (result.SampleProducts > 0)
                    ShowSampleSummary(result);

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
            if (IsUsSelected && ParsedTaxRate() == null) return "Enter the sales tax rate as a percentage between 0 and 30, e.g. 8.25.";
            if (IsUsSelected && !TryReadLoyalty(out _, out _, out var loyaltyError)) return loyaltyError;
            return null;
        }

        private string SelectedLanguage =>
            (ShopLanguage.SelectedItem as ComboBoxItem)?.Tag as string ?? ShopPhrases.English;

        private void LoadPhraseGrid(string language)
        {
            var custom = PhraseRow.CollectOverrides(_phrases);
            _phrases = PhraseRow.Load(language, custom);
            PhraseGrid.ItemsSource = _phrases;
        }

        private void Language_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || PhraseGrid == null) return;
            LoadPhraseGrid(SelectedLanguage);
            PhraseBook.Current.Apply(CurrentWording());
            RefreshSampleLabel();
        }

        private void RefreshSampleLabel()
        {
            if (LoadSamplesText == null) return;
            LoadSamplesText.Text = PhraseBook.Current["setup.samplesCheck"] + $" ({UsPharmacySampleData.Items.Count})";
        }

        private ShopTextSettings CurrentWording()
        {
            TryReadLoyalty(out var perDollar, out var perReward, out _);
            return new ShopTextSettings(SelectedLanguage, PhraseRow.CollectOverrides(_phrases), perDollar, perReward);
        }

        private bool TryReadLoyalty(out decimal perDollar, out int perReward, out string? error)
        {
            perDollar = 1m;
            perReward = 100;
            error = null;
            if (!IsUsSelected) return true;
            if (!decimal.TryParse(PointsPerDollar.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out perDollar) || perDollar < 0)
            {
                error = "Enter the loyalty points earned per dollar, for example 1.";
                return false;
            }
            if (!int.TryParse(PointsPerReward.Text.Trim(), out perReward) || perReward < 1)
            {
                error = "Enter how many points equal one dollar off, for example 100.";
                return false;
            }
            return true;
        }

        private async Task SaveWordingAsync()
        {
            var settings = CurrentWording();
            await _shopText.SaveAsync(settings);
            PhraseBook.Current.Apply(settings);
        }

        private bool IsUsSelected => Currency.SelectedItem is CurrencyOption c && RegionCodes.IsUnitedStates(c.RegionCode);

        /// <summary>The tax rate typed in, or null when it isn't a percentage from 0 to 30.</summary>
        private decimal? ParsedTaxRate() =>
            decimal.TryParse(TaxRate.Text.Replace("%", "").Trim(), System.Globalization.NumberStyles.Number,
                             System.Globalization.CultureInfo.InvariantCulture, out var rate) && rate >= 0 && rate <= 30
                ? rate
                : null;

        private void Currency_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TaxPanel != null)
                TaxPanel.Visibility = IsUsSelected ? Visibility.Visible : Visibility.Collapsed;
            if (LoyaltyPanel != null)
                LoyaltyPanel.Visibility = IsUsSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Lists what the sample data added, including the staff logins, which exist nowhere else.</summary>
        private void ShowSampleSummary(FirstRunSetupResult result)
        {
            var text = $"Sample data loaded: {result.SampleProducts} products and {result.SampleCustomers} charge-account customers. " +
                       "The most common items are on the sale screen's quick keys (F1–F10).";
            if (result.SampleLogins.Count > 0)
            {
                text += "\n\nSample staff logins for testing:\n" +
                        string.Join("\n", result.SampleLogins.Select(l => $"   {l.Username}  /  {l.Password}   ({l.Role})")) +
                        "\n\nSign in with your own owner account. Delete the sample logins in Admin → Users before going live.";
            }
            MessageBox.Show(this, text, "Setup complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorPanel.Visibility = Visibility.Visible;
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
