using System.Windows;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Lets each shop set its own receipt header (name/address/phone), footer lines, and
    /// regional settings (currency, ID document name, payroll deduction) without a rebuild.
    /// Values are stored machine-wide by <see cref="ReceiptBranding"/> and <see cref="Region"/>
    /// and picked up by every print document and screen in the app. This is what lets one
    /// build ship to a shop in any country.
    /// </summary>
    public partial class ReceiptSettingsWindow : Window
    {
        public ReceiptSettingsWindow()
        {
            InitializeComponent();
            LoadIntoFields(ReceiptBranding.Current);
            LoadRegionIntoFields(Region.Current);
            UpdatePreview();
        }

        private void LoadRegionIntoFields(RegionSettingsData r)
        {
            txtCurrencySymbol.Text = r.CurrencySymbol;
            txtCurrencyName.Text = r.CurrencyName;
            txtNationalIdLabel.Text = r.NationalIdLabel;
            txtStatutoryDeductionLabel.Text = r.StatutoryDeductionLabel;
            cboNumberWords.SelectedIndex = r.NumberWords == NumberWordStyle.SouthAsian ? 1 : 0;
        }

        private RegionSettingsData ReadRegionFromFields() => new()
        {
            CurrencySymbol = txtCurrencySymbol.Text.Trim(),
            CurrencyName = txtCurrencyName.Text.Trim(),
            NationalIdLabel = txtNationalIdLabel.Text.Trim(),
            StatutoryDeductionLabel = txtStatutoryDeductionLabel.Text.Trim(),
            NumberWords = cboNumberWords.SelectedIndex == 1
                ? NumberWordStyle.SouthAsian
                : NumberWordStyle.International
        };

        private void LoadIntoFields(ReceiptBrandingSettings s)
        {
            txtStoreName.Text = s.StoreName;
            txtStoreAddress.Text = s.StoreAddress;
            txtStorePhone.Text = s.StorePhone;
            txtHeaderNote.Text = s.HeaderNote;
            txtFooterMessage.Text = s.FooterMessage;
            txtFooterNote.Text = s.FooterNote;
            UpdatePreview();
        }

        private ReceiptBrandingSettings ReadFromFields() => new()
        {
            StoreName = txtStoreName.Text.Trim(),
            StoreAddress = txtStoreAddress.Text.Trim(),
            StorePhone = txtStorePhone.Text.Trim(),
            HeaderNote = txtHeaderNote.Text.Trim(),
            FooterMessage = txtFooterMessage.Text.Trim(),
            FooterNote = txtFooterNote.Text.Trim()
        };

        private void UpdatePreview()
        {
            var s = ReadFromFields();

            var headerLines = new[] { s.StoreName, s.StoreAddress, s.StorePhone, s.HeaderNote }
                .Where(l => !string.IsNullOrWhiteSpace(l));
            txtPreviewHeader.Text = string.Join(Environment.NewLine, headerLines);

            var footerLines = new[] { s.FooterMessage, s.FooterNote }
                .Where(l => !string.IsNullOrWhiteSpace(l));
            txtPreviewFooter.Text = string.Join(Environment.NewLine, footerLines);

            // Show the currency against a sample total so the symbol is checked before saving.
            var symbol = txtCurrencySymbol.Text.Trim();
            txtPreviewTotal.Text = $"TOTAL   {symbol} 1,250.00";
        }

        private void RefreshPreview_Click(object sender, RoutedEventArgs e) => UpdatePreview();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = ReadFromFields();

            if (string.IsNullOrWhiteSpace(settings.StoreName))
            {
                NotificationHelper.ValidationErrorCustom("Shop Name is required — it is the main line on every receipt.");
                txtStoreName.Focus();
                return;
            }

            var region = ReadRegionFromFields();

            if (string.IsNullOrWhiteSpace(region.CurrencySymbol))
            {
                NotificationHelper.ValidationErrorCustom("Currency Symbol is required — it is printed on every amount.");
                txtCurrencySymbol.Focus();
                return;
            }

            if (!ReceiptBranding.Save(settings) || !Region.Save(region))
            {
                NotificationHelper.ValidationErrorCustom(
                    "Could not save the settings. Try running the software as Administrator.");
                return;
            }

            UpdatePreview();
            txtStatus.Text = $"Saved at {DateTime.Now:hh:mm tt}. New printouts use these immediately; " +
                             "close and reopen the other windows to refresh their on-screen labels.";
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset the header, footer and regional settings back to the built-in defaults?",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Only fills the fields — nothing is written until Save is pressed.
            LoadIntoFields(new ReceiptBrandingSettings());
            LoadRegionIntoFields(new RegionSettingsData());
            UpdatePreview();
            txtStatus.Text = "Defaults loaded. Press Save to apply them.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
