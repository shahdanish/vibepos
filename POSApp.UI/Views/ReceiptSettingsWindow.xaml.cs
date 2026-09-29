using System.Windows;
using POSApp.UI.Helpers;

namespace POSApp.UI.Views
{
    /// <summary>
    /// Lets each shop set its own receipt header (name/address/phone) and footer lines
    /// without a rebuild. Values are stored machine-wide by <see cref="ReceiptBranding"/>
    /// and picked up by every print document in the app.
    /// </summary>
    public partial class ReceiptSettingsWindow : Window
    {
        public ReceiptSettingsWindow()
        {
            InitializeComponent();
            LoadIntoFields(ReceiptBranding.Current);
        }

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

            if (!ReceiptBranding.Save(settings))
            {
                NotificationHelper.ValidationErrorCustom(
                    "Could not save the receipt settings. Try running the software as Administrator.");
                return;
            }

            UpdatePreview();
            txtStatus.Text = $"Saved at {DateTime.Now:hh:mm tt}. New printouts will use these lines immediately.";
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Reset the header and footer lines back to the built-in defaults?",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Only fills the fields — nothing is written until Save is pressed.
            LoadIntoFields(new ReceiptBrandingSettings());
            txtStatus.Text = "Defaults loaded. Press Save to apply them.";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
