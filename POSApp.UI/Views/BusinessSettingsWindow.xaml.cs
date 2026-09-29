using System.Windows;
using System.Windows.Controls;
using POSApp.UI.Helpers;

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

            txtCurrencySymbol.Text = r.CurrencySymbol;
            txtCurrencyName.Text = r.CurrencyName;
            txtNationalIdLabel.Text = r.NationalIdLabel;
            txtStatutoryDeductionLabel.Text = r.StatutoryDeductionLabel;

            cboSymbolSide.SelectedIndex = r.SymbolSide == SymbolPosition.After ? 1 : 0;
            cboDecimals.SelectedIndex = Math.Clamp(r.DecimalPlaces, 0, 3);
            cboNumberFormat.SelectedIndex = (int)r.NumberFormat;
            cboNumberWords.SelectedIndex = r.NumberWords == NumberWordStyle.SouthAsian ? 1 : 0;
            cboDateStyle.SelectedIndex = (int)r.Dates;

            _loading = false;
            UpdatePreview();
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

        private RegionSettingsData ReadRegion() => new()
        {
            CurrencySymbol = txtCurrencySymbol.Text.Trim(),
            CurrencyName = txtCurrencyName.Text.Trim(),
            NationalIdLabel = txtNationalIdLabel.Text.Trim(),
            StatutoryDeductionLabel = txtStatutoryDeductionLabel.Text.Trim(),
            SymbolSide = cboSymbolSide.SelectedIndex == 1 ? SymbolPosition.After : SymbolPosition.Before,
            DecimalPlaces = Math.Clamp(cboDecimals.SelectedIndex, 0, 3),
            NumberFormat = (NumberFormatStyle)Math.Clamp(cboNumberFormat.SelectedIndex, 0, 3),
            NumberWords = cboNumberWords.SelectedIndex == 1 ? NumberWordStyle.SouthAsian : NumberWordStyle.International,
            Dates = (DateStyle)Math.Clamp(cboDateStyle.SelectedIndex, 0, 2)
        };

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

                var headerLines = new[] { b.StoreName, b.StoreAddress, b.StorePhone, b.HeaderNote }
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

                txtPreviewLabels.Text =
                    $"{Region.AmountLabelRequired}   ·   {Region.CostPriceLabel}   ·   {Region.BalanceLabel}\n" +
                    $"{Region.NationalIdLabel}   ·   {Region.StatutoryDeductionAmountLabel}   ·   " +
                    $"cash over/short {Region.MoneySigned(-75m)}\n" +
                    $"report date {Region.Date(sample)}   ·   {Region.LongDate(sample)}";
            }
            finally
            {
                Region.Apply(saved);
            }
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => UpdatePreview();

        private void Setting_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();

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

            if (!ReceiptBranding.Save(branding) || !Region.Save(region))
            {
                NotificationHelper.ValidationErrorCustom(
                    "Could not save the settings. Try running the software as Administrator.");
                return;
            }

            UpdatePreview();
            txtStatus.Text = $"Saved at {DateTime.Now:hh:mm tt}. Printouts use these immediately — " +
                             "close and reopen the other windows to refresh their on-screen labels.";
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset the shop details, currency and regional settings back to the built-in defaults?",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Only fills the fields — nothing is written until Save is pressed.
            LoadAll(new ReceiptBrandingSettings(), new RegionSettingsData());
            txtStatus.Text = "Defaults loaded. Press Save to apply them.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
