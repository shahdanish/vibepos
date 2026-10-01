using System.Windows;
using System.Windows.Controls;
using POSApp.UI.Helpers;
using POSApp.Core.Services;

namespace POSApp.UI.Views
{
    /// <summary>
    /// The single admin screen for everything that varies between shops and countries:
    /// shop identity, currency, number/date format and regional labels.
    ///
    /// Values are stored machine-wide by <see cref="ReceiptBranding"/> and <see cref="Region"/>,
    /// and read by every print document, window title and money/date field in the app. This is
    /// what lets one build ship to any client without a developer touching the code.
    /// </summary>
    public partial class BusinessSettingsWindow : Window
    {
        /// <summary>Set while loading so the ComboBox handlers do not redraw a half-filled form.</summary>
        private bool _loading;

        /// <summary>Country codes in the order of the Country drop-down.</summary>
        private static readonly string[] RegionCodeByIndex = { RegionCodes.Pakistan, RegionCodes.UnitedStates, RegionCodes.Other };

        /// <summary>Culture of the loaded settings or the last preset applied; not edited on screen.</summary>
        private string _culture = "en-PK";

        public BusinessSettingsWindow()
        {
            InitializeComponent();
            LoadAll(ReceiptBranding.Current, Region.Current);
        }

        // ── Load / read ───────────────────────────────────────────────────────

        private void LoadAll(ReceiptBrandingSettings b, RegionSettingsData r)
        {
            _loading = true;

            txtStoreName.Text = b.StoreName;
            txtStoreAddress.Text = b.StoreAddress;
            txtStorePhone.Text = b.StorePhone;
            txtHeaderNote.Text = b.HeaderNote;
            txtFooterMessage.Text = b.FooterMessage;
            txtFooterNote.Text = b.FooterNote;

            var regionIndex = Array.FindIndex(RegionCodeByIndex, c => string.Equals(c, r.RegionCode, StringComparison.OrdinalIgnoreCase));
            cboRegion.SelectedIndex = regionIndex >= 0 ? regionIndex : RegionCodeByIndex.Length - 1;
            LoadRegionFields(r);

            _loading = false;
            UpdatePreview();
        }

        /// <summary>Fills the currency and regional fields (not the country picker) from settings.</summary>
        private void LoadRegionFields(RegionSettingsData r)
        {
            var wasLoading = _loading;
            _loading = true;

            _culture = r.Culture;
            txtCurrencySymbol.Text = r.CurrencySymbol;
            txtCurrencyName.Text = r.CurrencyName;
            txtCurrencyCode.Text = r.CurrencyCode;
            txtNationalIdLabel.Text = r.NationalIdLabel;
            txtStatutoryDeductionLabel.Text = r.StatutoryDeductionLabel;

            cboSymbolSide.SelectedIndex = r.SymbolSide == SymbolPosition.After ? 1 : 0;
            cboSymbolSpacing.SelectedIndex = r.SymbolSpacing ? 0 : 1;
            cboDecimals.SelectedIndex = Math.Clamp(r.DecimalPlaces, 0, 3);
            cboCompactDecimals.SelectedIndex = r.CompactDecimalPlaces == 0 ? 0 : 1;
            cboNumberFormat.SelectedIndex = (int)r.NumberFormat;
            cboNumberWords.SelectedIndex = r.NumberWords == NumberWordStyle.SouthAsian ? 1 : 0;
            cboDateStyle.SelectedIndex = (int)r.Dates;
            cboTimeStyle.SelectedIndex = (int)r.Times;

            _loading = wasLoading;
        }

        private ReceiptBrandingSettings ReadBranding() => new()
        {
            StoreName = txtStoreName.Text.Trim(),
            StoreAddress = txtStoreAddress.Text.Trim(),
            StorePhone = txtStorePhone.Text.Trim(),
            HeaderNote = txtHeaderNote.Text.Trim(),
            FooterMessage = txtFooterMessage.Text.Trim(),
            FooterNote = txtFooterNote.Text.Trim()
        };

        private RegionSettingsData ReadRegion()
        {
            var decimals = Math.Clamp(cboDecimals.SelectedIndex, 0, 3);
            return new RegionSettingsData
            {
                RegionCode = RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)],
                Culture = _culture,
                CurrencySymbol = txtCurrencySymbol.Text.Trim(),
                CurrencyName = txtCurrencyName.Text.Trim(),
                CurrencyCode = txtCurrencyCode.Text.Trim().ToUpperInvariant(),
                NationalIdLabel = txtNationalIdLabel.Text.Trim(),
                StatutoryDeductionLabel = txtStatutoryDeductionLabel.Text.Trim(),
                SymbolSide = cboSymbolSide.SelectedIndex == 1 ? SymbolPosition.After : SymbolPosition.Before,
                SymbolSpacing = cboSymbolSpacing.SelectedIndex != 1,
                DecimalPlaces = decimals,
                // "With decimals" follows the currency's own decimal places.
                CompactDecimalPlaces = cboCompactDecimals.SelectedIndex == 1 ? decimals : 0,
                NumberFormat = (NumberFormatStyle)Math.Clamp(cboNumberFormat.SelectedIndex, 0, 3),
                NumberWords = cboNumberWords.SelectedIndex == 1 ? NumberWordStyle.SouthAsian : NumberWordStyle.International,
                Dates = (DateStyle)Math.Clamp(cboDateStyle.SelectedIndex, 0, 2),
                Times = (TimeStyle)Math.Clamp(cboTimeStyle.SelectedIndex, 0, 2)
            };
        }

        // ── Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Renders the sample receipt from the values on screen — which are not saved yet, so
        /// the settings are applied to <see cref="Region"/> only for the length of this method
        /// and the real ones are put straight back.
        /// </summary>
        private void UpdatePreview()
        {
            if (_loading) return;

            var b = ReadBranding();
            var draft = ReadRegion();

            if (string.IsNullOrWhiteSpace(draft.CurrencySymbol))
                draft.CurrencySymbol = "?";

            var saved = Region.Current;
            try
            {
                Region.Apply(draft);

                var headerLines = new[] { b.StoreName, b.StoreAddress, Region.Phone(b.StorePhone), b.HeaderNote }
                    .Where(l => !string.IsNullOrWhiteSpace(l));
                txtPreviewHeader.Text = string.Join(Environment.NewLine, headerLines);

                var sample = new DateTime(2026, 12, 31, 21, 30, 0);
                txtPreviewDate.Text = $"Invoice #1042      {Region.DateTimeText(sample)}";

                txtPreviewLine1.Text = $"Cooking Oil  2 × {Region.Money(450m)}   {Region.Money(900m)}";
                txtPreviewLine2.Text = $"Sugar 5kg    1 × {Region.Money(350.5m)}   {Region.Money(350.5m)}";
                txtPreviewTotal.Text = $"TOTAL   {Region.Money(1250.5m)}";
                txtPreviewWords.Text = Region.AmountInWords(1250.5m);

                var footerLines = new[] { b.FooterMessage, b.FooterNote }
                    .Where(l => !string.IsNullOrWhiteSpace(l));
                txtPreviewFooter.Text = string.Join(Environment.NewLine, footerLines);

                var samplePhone = string.IsNullOrWhiteSpace(b.StorePhone) ? "5555550123" : b.StorePhone;
                txtPreviewLabels.Text =
                    $"{Region.AmountLabelRequired}   ·   {Region.CostPriceLabel}   ·   {Region.BalanceLabel}\n" +
                    $"{Region.NationalIdLabel}   ·   {Region.StatutoryDeductionAmountLabel}   ·   " +
                    $"cash over/short {Region.MoneySigned(-75m)}\n" +
                    $"report date {Region.Date(sample)}   ·   {Region.LongDate(sample)}\n" +
                    $"price on a bill {Region.Compact(3.49m)}   ·   phone {Region.Phone(samplePhone)}";
            }
            finally
            {
                Region.Apply(saved);
            }
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => UpdatePreview();

        private void Setting_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

        /// <summary>
        /// Picking Pakistan or United States fills in that country's formats; "Other country"
        /// leaves every field as it is so a custom setup is never wiped.
        /// </summary>
        private void Region_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;

            var preset = RegionSettingsData.PresetFor(RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)]);
            if (preset != null)
            {
                LoadRegionFields(preset);
                txtStatus.Text = $"Filled in the usual settings for {((ComboBoxItem)cboRegion.SelectedItem).Content}. Press Save to apply them.";
            }

            UpdatePreview();
        }

        // ── Save / defaults ───────────────────────────────────────────────────

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var branding = ReadBranding();
            var region = ReadRegion();

            if (string.IsNullOrWhiteSpace(branding.StoreName))
            {
                NotificationHelper.ValidationErrorCustom("Shop Name is required — it is the main line on every receipt.");
                txtStoreName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(region.CurrencySymbol))
            {
                NotificationHelper.ValidationErrorCustom("Currency Symbol is required — it is printed on every amount.");
                txtCurrencySymbol.Focus();
                return;
            }

            if (region.CurrencyCode.Length != 3 || !region.CurrencyCode.All(char.IsAsciiLetterUpper))
            {
                NotificationHelper.ValidationErrorCustom("Currency Code must be the 3-letter ISO code, e.g. PKR or USD.");
                txtCurrencyCode.Focus();
                return;
            }

            if (!ReceiptBranding.Save(branding) || !Region.Save(region))
            {
                NotificationHelper.ValidationErrorCustom(
                    "Could not save the settings. Try running the software as Administrator.");
                return;
            }

            UpdatePreview();
            txtStatus.Text = $"Saved at {Region.Time(DateTime.Now)}. Printouts use these immediately — " +
                             "close and reopen the other windows to refresh their on-screen labels.";
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset the shop details, currency and regional settings back to the built-in defaults " +
                "for the country selected on the Regional tab?",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Only fills the fields — nothing is written until Save is pressed. "Other country"
            // has no preset of its own and falls back to the original defaults.
            var code = RegionCodeByIndex[Math.Clamp(cboRegion.SelectedIndex, 0, RegionCodeByIndex.Length - 1)];
            LoadAll(new ReceiptBrandingSettings(), RegionSettingsData.PresetFor(code) ?? new RegionSettingsData());
            txtStatus.Text = "Defaults loaded. Press Save to apply them.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
